/*
 * Authors:
 *   钟峰(Popeye Zhong) <zongsoft@gmail.com>
 *
 * Copyright (C) 2011-2014 Zongsoft Corporation <http://www.zongsoft.com>
 *
 * This file is part of Zongsoft.Web.Plugins.
 *
 * Zongsoft.Web.Plugins is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * Zongsoft.Web.Plugins is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with Zongsoft.Web.Plugins; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Routing;
using System.Web.Http;
using System.Web.Http.Routing;
using System.Web.Http.Dispatcher;
using System.Web.Http.Controllers;

namespace Zongsoft.Web.Plugins.Http
{
	public class HttpControllerActionSelector : System.Web.Http.Controllers.ApiControllerActionSelector
	{
		private ActionSelectorCacheItem _fastCache;
		private readonly object _cacheKey = new object();

		public override HttpActionDescriptor SelectAction(HttpControllerContext controllerContext)
		{
			if(controllerContext == null)
				throw new ArgumentNullException("controllerContext");

			ActionSelectorCacheItem internalSelector = GetInternalSelector(controllerContext.ControllerDescriptor);
			return internalSelector.SelectAction(controllerContext);
		}

		private ActionSelectorCacheItem GetInternalSelector(HttpControllerDescriptor controllerDescriptor)
		{
			// Performance-sensitive

			// First check in the local fast cache and if not a match then look in the broader 
			// HttpControllerDescriptor.Properties cache
			if(_fastCache == null)
			{
				ActionSelectorCacheItem selector = new ActionSelectorCacheItem(controllerDescriptor);
				System.Threading.Interlocked.CompareExchange(ref _fastCache, selector, null);
				return selector;
			}
			else if(_fastCache.HttpControllerDescriptor == controllerDescriptor)
			{
				// If the key matches and we already have the delegate for creating an instance then just execute it
				return _fastCache;
			}
			else
			{
				// If the key doesn't match then lookup/create delegate in the HttpControllerDescriptor.Properties for
				// that HttpControllerDescriptor instance
				object cacheValue;
				if(controllerDescriptor.Properties.TryGetValue(_cacheKey, out cacheValue))
				{
					return (ActionSelectorCacheItem)cacheValue;
				}
				// Race condition on initialization has no side effects
				ActionSelectorCacheItem selector = new ActionSelectorCacheItem(controllerDescriptor);
				controllerDescriptor.Properties.TryAdd(_cacheKey, selector);
				return selector;
			}
		}

		internal class ActionSelectorCacheItem
		{
			#region 私有变量
			private readonly HttpControllerDescriptor _controllerDescriptor;

			// Includes action descriptors for actionsByVerb with and without route attributes.
			private readonly CandidateAction[] _combinedCandidateActions;

			private readonly IDictionary<HttpActionDescriptor, string[]> _actionParameterNames = new Dictionary<HttpActionDescriptor, string[]>();

			// Includes action descriptors for actionsByVerb with and without route attributes.
			private readonly ILookup<string, HttpActionDescriptor> _combinedActionNameMapping;

			// Selection commonly looks up an action by verb.
			// Cache this mapping. These caches are completely optional and we still behave correctly if we cache miss.
			// We can adjust the specific set we cache based on profiler information.
			// Conceptually, this set of caches could be a HttpMethod --> ReflectedHttpActionDescriptor[].
			// - Beware that HttpMethod has a very slow hash function (it does case-insensitive string hashing). So don't use Dict.
			// - there are unbounded number of http methods, so make sure the cache doesn't grow indefinitely.
			// - we can build the cache at startup and don't need to continually add to it.
			private static readonly HttpMethod[] _cacheListVerbKinds = new HttpMethod[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Post };

			private StandardActionSelectionCache _standardActions;
			#endregion

			#region 构造函数
			public ActionSelectorCacheItem(HttpControllerDescriptor controllerDescriptor)
			{
				if(controllerDescriptor == null)
					throw new ArgumentNullException("controllerDescriptor");

				// Initialize the cache entirely in the ctor on a single thread.
				_controllerDescriptor = controllerDescriptor;

				MethodInfo[] allMethods = _controllerDescriptor.ControllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
				MethodInfo[] validMethods = Array.FindAll(allMethods, IsValidActionMethod);

				_combinedCandidateActions = new CandidateAction[validMethods.Length];
				for(int i = 0; i < validMethods.Length; i++)
				{
					MethodInfo method = validMethods[i];
					ReflectedHttpActionDescriptor actionDescriptor = new ReflectedHttpActionDescriptor(_controllerDescriptor, method);
					_combinedCandidateActions[i] = new CandidateAction
					{
						ActionDescriptor = actionDescriptor
					};
					HttpActionBinding actionBinding = actionDescriptor.ActionBinding;

					// Building an action parameter name mapping to compare against the URI parameters coming from the request. Here we only take into account required parameters that are simple types and come from URI.
					_actionParameterNames.Add(
						actionDescriptor,
						actionBinding.ParameterBindings
							.Where(binding => !binding.Descriptor.IsOptional && Utility.CanConvertFromString(binding.Descriptor.ParameterType))
							.Select(binding => binding.Descriptor.Prefix ?? binding.Descriptor.ParameterName).ToArray());
				}

				_combinedActionNameMapping =
					_combinedCandidateActions
					.Select(c => c.ActionDescriptor)
					.ToLookup(actionDesc => actionDesc.ActionName, StringComparer.OrdinalIgnoreCase);
			}
			#endregion

			#region 公共属性
			public HttpControllerDescriptor HttpControllerDescriptor
			{
				get
				{
					return _controllerDescriptor;
				}
			}
			#endregion

			private static string CreateAmbiguousMatchList(IEnumerable<CandidateActionWithParams> ambiguousCandidates)
			{
				StringBuilder exceptionMessageBuilder = new StringBuilder();

				foreach(CandidateActionWithParams candidate in ambiguousCandidates)
				{
					HttpActionDescriptor descriptor = candidate.ActionDescriptor;
					System.Diagnostics.Contracts.Contract.Assert(descriptor != null);

					string controllerTypeName;

					if(descriptor.ControllerDescriptor != null
						&& descriptor.ControllerDescriptor.ControllerType != null)
					{
						controllerTypeName = descriptor.ControllerDescriptor.ControllerType.FullName;
					}
					else
					{
						controllerTypeName = String.Empty;
					}

					exceptionMessageBuilder.AppendLine();
					exceptionMessageBuilder.Append(string.Format("{0} on type {1}", descriptor.ActionName, controllerTypeName));
				}

				return exceptionMessageBuilder.ToString();
			}

			public HttpActionDescriptor SelectAction(HttpControllerContext controllerContext)
			{
				InitializeStandardActions();

				var selectedCandidates = FindMatchingActions(controllerContext);

				switch(selectedCandidates.Count)
				{
					case 0:
						var response = controllerContext.Request.CreateErrorResponse(HttpStatusCode.NotFound,
										string.Format("No HTTP resource was found that matches the request URI '{0}'.", controllerContext.Request.RequestUri));

						throw new HttpResponseException(response);
					case 1:
						var selectedCandidate = selectedCandidates.First();
						controllerContext.RouteData = selectedCandidate.RouteDataSource;
						return selectedCandidate.ActionDescriptor;
					default:
						var actionName = controllerContext.Request.Method.Method;
						object routeValue;

						if(controllerContext.RouteData.Values.TryGetValue("action", out routeValue) && routeValue is string)
							actionName = (string)routeValue;

						var a = selectedCandidates.FirstOrDefault(p => string.Equals(p.ActionDescriptor.ActionName, actionName, StringComparison.OrdinalIgnoreCase));
						if(a != null)
						{
							controllerContext.RouteData = a.RouteDataSource;
							return a.ActionDescriptor;
						}

						// Throws exception because multiple actionsByVerb match the request
						string ambiguityList = CreateAmbiguousMatchList(selectedCandidates);
						throw new InvalidOperationException(string.Format("Multiple actions were found that match the request: {0}", ambiguityList));
				}
			}

			// This method lazy-initializes the data needed for action selection. This is a safe race-condition. This is
			// done because we don't know whether or not an action/controller is attribute routed until after attribute
			// routes are added.
			private void InitializeStandardActions()
			{
				if(_standardActions != null)
					return;

				StandardActionSelectionCache standardActions = new StandardActionSelectionCache();

				if(Utility.IsAttributeRouted(_controllerDescriptor))
				{
					// The controller has an attribute route; no actionsByVerb are accessible via standard routing.
					standardActions.StandardCandidateActions = new CandidateAction[0];
				}
				else
				{
					// The controller does not have an attribute route; some actionsByVerb may be accessible via standard
					// routing.
					List<CandidateAction> standardCandidateActions = new List<CandidateAction>();

					for(int i = 0; i < _combinedCandidateActions.Length; i++)
					{
						CandidateAction candidate = _combinedCandidateActions[i];

						// We know that this cast is safe before we created all of the action descriptors for standard actions
						ReflectedHttpActionDescriptor action = (ReflectedHttpActionDescriptor)candidate.ActionDescriptor;

						// Allow standard routes access inherited actionsByVerb or actionsByVerb without Route attributes.
						if(action.MethodInfo.DeclaringType != _controllerDescriptor.ControllerType || !Utility.IsAttributeRouted(candidate.ActionDescriptor))
						{
							standardCandidateActions.Add(candidate);
						}
					}

					standardActions.StandardCandidateActions = standardCandidateActions.ToArray();
				}

				standardActions.StandardActionNameMapping =
					standardActions.StandardCandidateActions
					.Select(c => c.ActionDescriptor)
					.ToLookup(actionDesc => actionDesc.ActionName, StringComparer.OrdinalIgnoreCase);

				// Bucket the action descriptors by common verbs.
				int len = _cacheListVerbKinds.Length;
				standardActions.CacheListVerbs = new CandidateAction[len][];
				for(int i = 0; i < len; i++)
				{
					standardActions.CacheListVerbs[i] = FindActionsForVerbWorker(_cacheListVerbKinds[i], standardActions.StandardCandidateActions);
				}

				_standardActions = standardActions;
			}

			// Find all actionsByVerb on this controller that match the request. 
			// if ignoreVerbs = true, then don't filter actionsByVerb based on mismatching Http verb. This is useful for detecting 404/405. 
			private ICollection<CandidateActionWithParams> FindMatchingActions(HttpControllerContext controllerContext, bool ignoreVerbs = false)
			{
				// If matched with direct route?
				IHttpRouteData routeData = controllerContext.RouteData;
				IEnumerable<IHttpRouteData> subRoutes = Utility.GetSubRoutes(routeData);

				IEnumerable<CandidateActionWithParams> actionsWithParameters = (subRoutes == null) ?
					GetInitialCandidateWithParameterListForRegularRoutes(controllerContext, ignoreVerbs) :
					GetInitialCandidateWithParameterListForDirectRoutes(controllerContext, subRoutes, ignoreVerbs);

				// Make sure the action parameter matches the route and query parameters.
				var actionsFoundByParams = FindActionMatchRequiredRouteAndQueryParameters(actionsWithParameters);

				var orderCandidates = RunOrderFilter(actionsFoundByParams);
				var precedenceCandidates = RunPrecedenceFilter(orderCandidates);

				// Overload resolution logic is applied when needed.
				var selectedCandidates = FindActionMatchMostRouteAndQueryParameters(precedenceCandidates);

				return selectedCandidates;
			}

			private static bool IsValidActionMethod(MethodInfo methodInfo)
			{
				if(methodInfo.IsSpecialName)
				{
					// not a normal method, e.g. a constructor or an event
					return false;
				}

				if(methodInfo.GetBaseDefinition().DeclaringType.IsAssignableFrom(typeof(ApiController)))
				{
					// is a method on Object, IHttpController, ApiController
					return false;
				}

				if(methodInfo.GetCustomAttribute<NonActionAttribute>() != null)
				{
					return false;
				}

				return true;
			}

			// Given a list of actionsByVerb, filter it to ones that match a given verb. This can match by name or IActionHttpMethodSelector.
			// Since this list is fixed for a given verb type, it can be pre-computed and cached.
			// This function should not do caching. It's the helper that builds the caches.
			private static CandidateAction[] FindActionsForVerbWorker(HttpMethod verb, CandidateAction[] candidates)
			{
				List<CandidateAction> listCandidates = new List<CandidateAction>();

				FindActionsForVerbWorker(verb, candidates, listCandidates);

				return listCandidates.ToArray();
			}

			// Adds to existing list rather than send back as a return value.
			private static void FindActionsForVerbWorker(HttpMethod verb, CandidateAction[] candidates, List<CandidateAction> listCandidates)
			{
				foreach(CandidateAction candidate in candidates)
				{
					if(candidate.ActionDescriptor != null && candidate.ActionDescriptor.SupportedHttpMethods.Contains(verb))
					{
						listCandidates.Add(candidate);
					}
				}
			}

			// Call for direct routes. 
			private static List<CandidateActionWithParams> GetInitialCandidateWithParameterListForDirectRoutes(HttpControllerContext controllerContext, IEnumerable<IHttpRouteData> subRoutes, bool ignoreVerbs)
			{
				HttpRequestMessage request = controllerContext.Request;
				HttpMethod incomingMethod = controllerContext.Request.Method;

				var queryNameValuePairs = request.GetQueryNameValuePairs();

				List<CandidateActionWithParams> candidateActionWithParams = new List<CandidateActionWithParams>();

				foreach(IHttpRouteData subRouteData in subRoutes)
				{
					// Each route may have different route parameters.
					ISet<string> combinedParameterNames = GetCombinedParameterNames(queryNameValuePairs, subRouteData.Values);

					CandidateAction[] candidates = Utility.GetDirectRouteCandidates(subRouteData.Route);

					string actionName;
					subRouteData.Values.TryGetValue("action", out actionName);

					foreach(var candidate in candidates)
					{
						if((actionName == null) || candidate.MatchName(actionName))
						{
							if(ignoreVerbs || candidate.MatchVerb(incomingMethod))
							{
								candidateActionWithParams.Add(new CandidateActionWithParams(candidate, combinedParameterNames, subRouteData));
							}
						}
					}
				}
				return candidateActionWithParams;
			}

			// Call for non-direct routes
			private IEnumerable<CandidateActionWithParams> GetInitialCandidateWithParameterListForRegularRoutes(HttpControllerContext controllerContext, bool ignoreVerbs = false)
			{
				CandidateAction[] candidates = GetInitialCandidateList(controllerContext, ignoreVerbs);
				return GetCandidateActionsWithBindings(controllerContext, candidates);
			}

			private CandidateAction[] GetInitialCandidateList(HttpControllerContext controllerContext, bool ignoreVerbs = false)
			{
				// Initial candidate list is determined by:
				// - Direct route?
				// - {action} value?
				// - ignore verbs?
				string actionName;

				HttpMethod incomingMethod = controllerContext.Request.Method;
				IHttpRouteData routeData = controllerContext.RouteData;

				System.Diagnostics.Contracts.Contract.Assert(Utility.GetSubRoutes(routeData) == null, "Should not be called on a direct route");
				CandidateAction[] candidates;

				if(routeData.Values.TryGetValue("action", out actionName))
				{
					// We have an explicit {action} value, do traditional binding. Just lookup by actionName
					HttpActionDescriptor[] actionsFoundByName = _standardActions.StandardActionNameMapping[actionName].ToArray();

					// Throws HttpResponseException with NotFound status because no action matches the Name
					if(actionsFoundByName.Length == 0)
					{
						var response = controllerContext.Request.CreateErrorResponse(
							HttpStatusCode.NotFound,
							string.Format("No action was found on the controller '{0}' that matches the name '{1}'.", _controllerDescriptor.ControllerName, actionName));

						throw new HttpResponseException(response);
					}

					CandidateAction[] candidatesFoundByName = new CandidateAction[actionsFoundByName.Length];

					for(int i = 0; i < actionsFoundByName.Length; i++)
					{
						candidatesFoundByName[i] = new CandidateAction
						{
							ActionDescriptor = actionsFoundByName[i]
						};
					}

					if(ignoreVerbs)
					{
						candidates = candidatesFoundByName;
					}
					else
					{
						candidates = FilterIncompatibleVerbs(incomingMethod, candidatesFoundByName);
					}
				}
				else
				{
					if(ignoreVerbs)
					{
						candidates = _standardActions.StandardCandidateActions;
					}
					else
					{
						// No direct routing or {action} parameter, infer it from the verb.
						candidates = FindActionsForVerb(incomingMethod, _standardActions.CacheListVerbs, _standardActions.StandardCandidateActions);
					}
				}

				return candidates;
			}

			private static CandidateAction[] FilterIncompatibleVerbs(HttpMethod incomingMethod, CandidateAction[] candidatesFoundByName)
			{
				return candidatesFoundByName.Where(candidate => candidate.ActionDescriptor.SupportedHttpMethods.Contains(incomingMethod)).ToArray();
			}

			// This is called when we don't specify an Action name
			// Get list of actionsByVerb that match a given verb. This can match by name or IActionHttpMethodSelector
			private static CandidateAction[] FindActionsForVerb(HttpMethod verb, CandidateAction[][] actionsByVerb, CandidateAction[] otherActions)
			{
				// Check cache for common verbs.
				for(int i = 0; i < _cacheListVerbKinds.Length; i++)
				{
					// verb selection on common verbs is normalized to have object reference identity.
					// This is significantly more efficient than comparing the verbs based on strings.
					if(Object.ReferenceEquals(verb, _cacheListVerbKinds[i]))
					{
						return actionsByVerb[i];
					}
				}

				// General case for any verbs.
				return FindActionsForVerbWorker(verb, otherActions);
			}

			// Given a list of candidate actionsByVerb, return a parallel list that includes the parameter information. 
			// This is used for regular routing where all candidates come from a single route, so they all share the same route parameter names. 
			private static CandidateActionWithParams[] GetCandidateActionsWithBindings(HttpControllerContext controllerContext, CandidateAction[] candidatesFound)
			{
				HttpRequestMessage request = controllerContext.Request;
				var queryNameValuePairs = request.GetQueryNameValuePairs();
				IHttpRouteData routeData = controllerContext.RouteData;
				IDictionary<string, object> routeValues = routeData.Values;
				ISet<string> combinedParameterNames = GetCombinedParameterNames(queryNameValuePairs, routeValues);

				CandidateActionWithParams[] candidatesWithParams = Array.ConvertAll(candidatesFound, candidate => new CandidateActionWithParams(candidate, combinedParameterNames, routeData));
				return candidatesWithParams;
			}

			// Get a non-null set that combines both the route and query parameters. 
			private static ISet<string> GetCombinedParameterNames(IEnumerable<KeyValuePair<string, string>> queryNameValuePairs, IDictionary<string, object> routeValues)
			{
				HashSet<string> routeParameterNames = new HashSet<string>(routeValues.Keys, StringComparer.OrdinalIgnoreCase);
				routeParameterNames.Remove("controller");
				routeParameterNames.Remove("action");

				var combinedParameterNames = new HashSet<string>(routeParameterNames, StringComparer.OrdinalIgnoreCase);
				if(queryNameValuePairs != null)
				{
					foreach(var queryNameValuePair in queryNameValuePairs)
					{
						combinedParameterNames.Add(queryNameValuePair.Key);
					}
				}
				return combinedParameterNames;
			}

			private ICollection<CandidateActionWithParams> FindActionMatchRequiredRouteAndQueryParameters(IEnumerable<CandidateActionWithParams> candidatesFound)
			{
				List<CandidateActionWithParams> matches = new List<CandidateActionWithParams>();

				foreach(var candidate in candidatesFound)
				{
					HttpActionDescriptor descriptor = candidate.ActionDescriptor;
					if(IsSubset(_actionParameterNames[descriptor], candidate.CombinedParameterNames))
					{
						matches.Add(candidate);
					}
				}

				return matches;
			}

			private ICollection<CandidateActionWithParams> FindActionMatchMostRouteAndQueryParameters(ICollection<CandidateActionWithParams> candidatesFound)
			{
				if(candidatesFound.Count > 1)
				{
					// select the results that match the most number of required parameters
					return candidatesFound
						.GroupBy(candidate => _actionParameterNames[candidate.ActionDescriptor].Length)
						.OrderByDescending(g => g.Key)
						.First()
						.ToArray();
				}

				return candidatesFound;
			}

			private static bool IsSubset(string[] actionParameters, ISet<string> routeAndQueryParameters)
			{
				foreach(string actionParameter in actionParameters)
				{
					if(!routeAndQueryParameters.Contains(actionParameter))
					{
						return false;
					}
				}

				return true;
			}

			private static ICollection<CandidateActionWithParams> RunOrderFilter(ICollection<CandidateActionWithParams> candidatesFound)
			{
				if(candidatesFound.Count == 0)
				{
					return candidatesFound;
				}
				int minOrder = candidatesFound.Min(c => c.CandidateAction.Order);
				return candidatesFound.Where(c => c.CandidateAction.Order == minOrder).ToList();
			}

			private static ICollection<CandidateActionWithParams> RunPrecedenceFilter(ICollection<CandidateActionWithParams> candidatesFound)
			{
				if(candidatesFound.Count == 0)
				{
					return candidatesFound;
				}
				decimal highestPrecedence = candidatesFound.Min(c => c.CandidateAction.Precedence);
				return candidatesFound.Where(c => c.CandidateAction.Precedence == highestPrecedence).ToList();
			}

			// Associate parameter (route and query) with each action. 
			// For regular routing, there was just a single route, and so single set of route parameters and so all of these
			// may share the same set of combined parameter names.
			// For attribute routing, there may be multiple routes, each with different route parameter names, and 
			// so each instance of a CandidateActionWithParams may have a different parameter set.
			private class CandidateActionWithParams
			{
				public CandidateActionWithParams(CandidateAction candidateAction, ISet<string> parameters, IHttpRouteData routeDataSource)
				{
					CandidateAction = candidateAction;
					CombinedParameterNames = parameters;
					RouteDataSource = routeDataSource;
				}

				public CandidateAction CandidateAction
				{
					get;
					private set;
				}

				public ISet<string> CombinedParameterNames
				{
					get;
					private set;
				}

				// Remember this so that we can apply it for model binding. 
				public IHttpRouteData RouteDataSource
				{
					get;
					private set;
				}

				public HttpActionDescriptor ActionDescriptor
				{
					get
					{
						return CandidateAction.ActionDescriptor;
					}
				}

				private string DebuggerToString()
				{
					StringBuilder sb = new StringBuilder();
					sb.Append(CandidateAction.DebuggerToString());
					if(CombinedParameterNames.Count > 0)
					{
						sb.Append(", Params =");
						foreach(string param in CombinedParameterNames)
						{
							sb.AppendFormat(" {0}", param);
						}
					}
					return sb.ToString();
				}
			}

			// A cache of the 'standard actions' for a controller - the actions that are reachable via traditional routes.
			private class StandardActionSelectionCache
			{
				// Includes action descriptors only for actions accessible via standard routing (without route attributes).
				public ILookup<string, HttpActionDescriptor> StandardActionNameMapping
				{
					get;
					set;
				}

				// Includes action descriptors only for actions accessible via standard routing (without route attributes).
				public CandidateAction[] StandardCandidateActions
				{
					get;
					set;
				}

				public CandidateAction[][] CacheListVerbs
				{
					get;
					set;
				}
			}
		}

		internal class CandidateAction
		{
			public HttpActionDescriptor ActionDescriptor
			{
				get;
				set;
			}

			public int Order
			{
				get;
				set;
			}

			public decimal Precedence
			{
				get;
				set;
			}

			public bool MatchName(string actionName)
			{
				return String.Equals(ActionDescriptor.ActionName, actionName, StringComparison.OrdinalIgnoreCase);
			}

			public bool MatchVerb(HttpMethod method)
			{
				return ActionDescriptor.SupportedHttpMethods.Contains(method);
			}

			internal string DebuggerToString()
			{
				return String.Format(System.Globalization.CultureInfo.CurrentCulture, "{0}, Order={1}, Prec={2}", ActionDescriptor.ActionName, Order, Precedence);
			}
		}

		internal static class Utility
		{
			private const string SubRouteDataKey = "MS_SubRoutes";
			private const string AttributeRoutedPropertyKey = "MS_IsAttributeRouted";

			public static bool IsAttributeRouted(HttpActionDescriptor actionDescriptor)
			{
				if(actionDescriptor == null)
				{
					throw new ArgumentNullException("actionDescriptor");
				}

				object value;
				actionDescriptor.Properties.TryGetValue(AttributeRoutedPropertyKey, out value);
				return value as bool? ?? false;
			}

			public static bool IsAttributeRouted(HttpControllerDescriptor controllerDescriptor)
			{
				if(controllerDescriptor == null)
					throw new ArgumentNullException("controllerDescriptor");

				object value;
				controllerDescriptor.Properties.TryGetValue(AttributeRoutedPropertyKey, out value);
				return value as bool ? ?? false;
			}

			//public static bool WillReadUri(HttpParameterBinding parameterBinding)
			//{
			//	if(parameterBinding == null)
			//		throw new ArgumentNullException("parameterBinding");

			//	var valueProviderParameterBinding = parameterBinding as System.Web.Http.ModelBinding.IValueProviderParameterBinding;
			//	if(valueProviderParameterBinding != null)
			//	{
			//		var valueProviderFactories = valueProviderParameterBinding.ValueProviderFactories;
			//		if(valueProviderFactories.Any() && valueProviderFactories.All(factory => factory is System.Web.Http.ValueProviders.IUriValueProviderFactory))
			//		{
			//			return true;
			//		}
			//	}

			//	return false;
			//}

			public static IEnumerable<IHttpRouteData> GetSubRoutes(IHttpRouteData routeData)
			{
				object value;

				if(routeData.Values.TryGetValue(SubRouteDataKey, out value))
				{
					return value as IHttpRouteData[];
				}

				return null;
			}

			internal static bool IsSimpleType(Type type)
			{
				return type.IsPrimitive ||
					   type.Equals(typeof(string)) ||
					   type.Equals(typeof(DateTime)) ||
					   type.Equals(typeof(Decimal)) ||
					   type.Equals(typeof(Guid)) ||
					   type.Equals(typeof(DateTimeOffset)) ||
					   type.Equals(typeof(TimeSpan));
			}

			internal static bool IsSimpleUnderlyingType(Type type)
			{
				Type underlyingType = Nullable.GetUnderlyingType(type);
				if(underlyingType != null)
				{
					type = underlyingType;
				}

				return Utility.IsSimpleType(type);
			}

			internal static bool CanConvertFromString(Type type)
			{
				return Utility.IsSimpleUnderlyingType(type) || Utility.HasStringConverter(type);
			}

			internal static bool HasStringConverter(Type type)
			{
				return System.ComponentModel.TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));
			}

			internal static class RouteDataTokenKeys
			{
				// Used to provide the action descriptors to consider for attribute routing
				public const string Actions = "actions";

				// Used to indicate that a route is a controller-level attribute route.
				public const string Controller = "controller";

				// Used to allow customer-provided disambiguation between multiple matching attribute routes
				public const string Order = "order";

				// Used to allow URI constraint-based disambiguation between multiple matching attribute routes
				public const string Precedence = "precedence";
			}

			// If route is a direct route, get the action descriptors, order and precedence it may map to.
			public static CandidateAction[] GetDirectRouteCandidates(IHttpRoute route)
			{
				System.Diagnostics.Contracts.Contract.Assert(route != null);

				IDictionary<string, object> dataTokens = route.DataTokens;
				if(dataTokens == null)
				{
					return null;
				}

				List<CandidateAction> candidates = new List<CandidateAction>();

				HttpActionDescriptor[] directRouteActions = null;
				HttpActionDescriptor[] possibleDirectRouteActions;

				if(dataTokens.TryGetValue<HttpActionDescriptor[]>(RouteDataTokenKeys.Actions, out possibleDirectRouteActions))
				{
					if(possibleDirectRouteActions != null && possibleDirectRouteActions.Length > 0)
					{
						directRouteActions = possibleDirectRouteActions;
					}
				}

				if(directRouteActions == null)
				{
					return null;
				}

				int order = 0;
				int possibleOrder;
				if(dataTokens.TryGetValue<int>(RouteDataTokenKeys.Order, out possibleOrder))
				{
					order = possibleOrder;
				}

				decimal precedence = 0M;
				decimal possiblePrecedence;

				if(dataTokens.TryGetValue<decimal>(RouteDataTokenKeys.Precedence, out possiblePrecedence))
				{
					precedence = possiblePrecedence;
				}

				foreach(HttpActionDescriptor actionDescriptor in directRouteActions)
				{
					candidates.Add(new CandidateAction
					{
						ActionDescriptor = actionDescriptor,
						Order = order,
						Precedence = precedence
					});
				}

				return candidates.ToArray();
			}

		}
	}
}
