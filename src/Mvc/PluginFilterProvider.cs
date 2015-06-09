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
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

using Zongsoft.Plugins;

namespace Zongsoft.Web.Plugins.Mvc
{
	/// <summary>
	/// 支持由插件注入的MVC过滤器提供程序。
	/// </summary>
	/// <remarks>
	///		<para>全局过滤器挂载地址位于：/Workspace/Filters 插件路径下；控制器(Controller)和操作(Action)级的过滤器挂载地址位于：/Workspace/Filters/ + $(Area) + $(ControllerName) 插件路径下。</para>
	///		<para>控制器(Controller)和操作(Action)级过滤器的区别依据为构件属性集中是否包含不为空的action这个属性，如果该属性为空或不存在则表示为控制器级的过滤器。</para>
	///		<example>
	///		<![CDATA[
	///		<extension path="/Workspace/Filters/Tollgates/VehiclePassing">
	///			<object name="controller-action-filter"
	///			        type="AAA.BBB.XXXFilter, assemblyName"
	///			        order="0"
	///			        action="ActionName"
	///			        method="POST|GET|DELETE|PUT" />
	///		</extension>
	///
	///		<extension path="/Workspace/Filters">
	///			<object name="global-filter"
	///			        type="AAA.BBB.XXXFilter, assemblyName"
	///			        order="0"
	///			        method="POST|GET|DELETE|PUT" />
	///		</extension>
	///		]]>
	///		</example>
	/// </remarks>
	public class PluginFilterProvider : IFilterProvider
	{
		#region 常量定义
		private const string ROOT_FILTERS_PATH = "/Workspace/Filters";
		#endregion

		#region 成员字段
		private PluginContext _pluginContext;
		#endregion

		#region 构造函数
		public PluginFilterProvider(PluginContext pluginContext)
		{
			if(pluginContext == null)
				throw new ArgumentNullException("pluginContext");

			_pluginContext = pluginContext;
		}
		#endregion

		#region 公共方法
		public IEnumerable<Filter> GetFilters(ControllerContext controllerContext, ActionDescriptor actionDescriptor)
		{
			var filters = new List<Filter>();
			var filtersNode = _pluginContext.PluginTree.Find(ROOT_FILTERS_PATH);

			if(filtersNode != null)
			{
				foreach(var filterNode in filtersNode.Children)
				{
					if(this.ContainsHttpMethod(filterNode.Properties.GetRawValue("method"), controllerContext.HttpContext.Request.HttpMethod))
						filters.Add(this.GetFilter(filterNode, FilterScope.Global));
				}
			}

			var area = (string)controllerContext.RouteData.DataTokens["area"];
			var controllerName = controllerContext.RouteData.GetRequiredString("controller");

			filtersNode = _pluginContext.PluginTree.Find(ROOT_FILTERS_PATH, area, controllerName);

			if(filtersNode != null)
			{
				string actionName;

				foreach(var filterNode in filtersNode.Children)
				{
					if(!this.ContainsHttpMethod(filterNode.Properties.GetRawValue("method"), controllerContext.HttpContext.Request.HttpMethod))
						continue;

					if(filterNode.Properties.TryGetValue("action", out actionName))
					{
						if(string.Equals(actionDescriptor.ActionName, actionName, StringComparison.OrdinalIgnoreCase))
							filters.Add(this.GetFilter(filterNode, FilterScope.Action));
					}
					else
					{
						filters.Add(this.GetFilter(filterNode, FilterScope.Controller));
					}
				}
			}

			return filters.ToArray();
		}
		#endregion

		#region 私有方法
		private Filter GetFilter(PluginTreeNode node, FilterScope scope)
		{
			return node.UnwrapValue<Filter>(ObtainMode.Auto, null, ctx =>
			{
				var instance = PluginUtility.BuildBuiltin(ctx.Builtin, new string[]{ "order", "action", "method"});

				if(instance == null)
					return;

				ctx.Result = new Filter(instance,
				                 scope,
				                 ctx.Builtin.Properties.GetValue<int>("order", Filter.DefaultOrder));
			});
		}

		private bool ContainsHttpMethod(string testMethod, string requestMethod)
		{
			if(string.IsNullOrWhiteSpace(testMethod) || testMethod.Trim() == "*")
				return true;

			var parts = testMethod.Split(',');

			return parts.Any(part => string.Equals(part.Trim(), requestMethod.Trim(), StringComparison.OrdinalIgnoreCase));
		}
		#endregion
	}
}
