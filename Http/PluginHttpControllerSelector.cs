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
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Web.Http.Routing;
using System.Web.Http.Dispatcher;
using System.Web.Http.Controllers;

namespace Zongsoft.Web.Plugins.Http
{
	public class PluginHttpControllerSelector : IHttpControllerSelector
	{
		#region 成员字段
		private Zongsoft.Plugins.PluginContext _pluginContext;
		#endregion

		#region 构造函数
		public PluginHttpControllerSelector(Zongsoft.Plugins.PluginContext pluginContext)
		{
			if(pluginContext == null)
				throw new ArgumentNullException("pluginContext");

			_pluginContext = pluginContext;
		}
		#endregion

		#region 公共方法
		public IDictionary<string, HttpControllerDescriptor> GetControllerMapping()
		{
			throw new NotImplementedException();
		}

		public HttpControllerDescriptor SelectController(HttpRequestMessage request)
		{
			var routeData = (IHttpRouteData)request.Properties[System.Web.Http.Hosting.HttpPropertyKeys.HttpRouteDataKey];
			object area;

			if(!routeData.Route.DataTokens.TryGetValue("area", out area))
				area = VirtualPathUtility.GetArea(routeData.Route.RouteTemplate);

			string controllerPath = Zongsoft.Plugins.PluginPath.Combine(string.IsNullOrEmpty((string)area) ? "Workbench" : (string)area, "Controllers", (string)routeData.Values["controller"]);

			var node = _pluginContext.PluginTree.Find(controllerPath);
			return new PluginHttpControllerDescriptor(request.GetConfiguration(), node);
		}
		#endregion
	}
}
