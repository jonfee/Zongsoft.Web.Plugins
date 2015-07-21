/*
 * Authors:
 *   钟峰(Popeye Zhong) <zongsoft@gmail.com>
 *
 * Copyright (C) 2011-2013 Zongsoft Corporation <http://www.zongsoft.com>
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
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
using System.Text;
using System.Web.Routing;

using Zongsoft.Plugins;
using Zongsoft.Plugins.Builders;

namespace Zongsoft.Web.Plugins.Builders
{
	public class RouteBuilder : Zongsoft.Plugins.Builders.BuilderBase
	{
		#region 重写方法
		public override object Build(BuilderContext context)
		{
			Builtin builtin = context.Builtin;

			string url = builtin.Properties.GetRawValue("url");
			var defaults = ResolveValues(builtin.Properties.GetValue<string>("defaults"), builtin);
			var dataTokens = ResolveValues(builtin.Properties.GetValue<string>("dataTokens"), builtin);
			var constraints = ResolveValues(builtin.Properties.GetValue<string>("constraints"), builtin);

			if(!dataTokens.ContainsKey("area"))
			{
				object area = null;

				if(constraints.TryGetValue("area", out area))
					dataTokens.Add("area", area);
				else
					dataTokens.Add("area", VirtualPathUtility.GetArea(url));
			}

			if(this.IsApiUrl(url))
			{
				return System.Web.Http.GlobalConfiguration.Configuration.Routes.CreateRoute(
								url,
								defaults,
								constraints,
								dataTokens);
			}
			else
			{
				return new Route(url,
								 defaults,
								 constraints,
								 dataTokens,
								 builtin.Properties.GetValue<IRouteHandler>("handler", new System.Web.Mvc.MvcRouteHandler()));
			}
		}

		protected override void OnBuilt(BuilderContext context)
		{
			if(context.Result is RouteBase)
			{
				var routes = RouteTable.Routes;

				if(context.Owner is RouteCollection)
					routes = (RouteCollection)context.Owner;

				routes.Add(context.Builtin.Name, (RouteBase)context.Result);
			}
			else if(context.Result is System.Web.Http.Routing.IHttpRoute)
			{
				var routes = System.Web.Http.GlobalConfiguration.Configuration.Routes;

				if(context.Owner is System.Web.Http.HttpRouteCollection)
					routes = (System.Web.Http.HttpRouteCollection)context.Owner;

				routes.Add(context.Builtin.Name, (System.Web.Http.Routing.IHttpRoute)context.Result);
			}
			else
				base.OnBuilt(context);
		}
		#endregion

		#region 保护方法
		protected bool IsApiUrl(string url)
		{
			if(string.IsNullOrWhiteSpace(url))
				return false;

			url = url.TrimStart();

			return url.StartsWith("api/", StringComparison.OrdinalIgnoreCase) ||
				   url.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
		}

		protected RouteValueDictionary ResolveValues(string text, Builtin builtin)
		{
			RouteValueDictionary result = new RouteValueDictionary();

			if(string.IsNullOrWhiteSpace(text))
				return result;

			var parts = text.Split(',');

			foreach(string part in parts)
			{
				int index = part.IndexOf('=');

				if(index > 0)
				{
					string key = part.Substring(0, index).Trim();

					if(!string.IsNullOrEmpty(key))
					{
						string valueText = part.Substring(index + 1);

						if(string.IsNullOrWhiteSpace(valueText))
							result.Add(key, System.Web.Mvc.UrlParameter.Optional);
						else
							result.Add(key, valueText);
					}
				}
			}

			return result;
		}
		#endregion
	}
}
