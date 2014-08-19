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
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Zongsoft.Web.Plugins.Mvc
{
	public class PluginExplorerController : Controller
	{
		#region 成员变量
		private Zongsoft.Plugins.PluginContext _pluginContext;
		#endregion

		#region 构造函数
		public PluginExplorerController(Zongsoft.Plugins.PluginContext pluginContext)
		{
			_pluginContext = pluginContext;
		}
		#endregion

		#region 行为方法
		public ActionResult Index(string path)
		{
			this.ViewData["PluginContext"] = _pluginContext;
			this.ViewData["PluginTree"] = _pluginContext.PluginTree;
			this.ViewData["Path"] = path == null ? string.Empty : path.Trim();

			var node = _pluginContext.PluginTree.RootNode;

			if(!string.IsNullOrWhiteSpace(path))
				node = _pluginContext.PluginTree.Find(path) ?? _pluginContext.PluginTree.RootNode;

			if(node == null)
			{
				this.ViewData["Target"] = null;
				this.ViewData["Target-Properties"] = null;
			}
			else
			{
				this.ViewData["Target"] = node.UnwrapValue(Zongsoft.Plugins.ObtainMode.Never, null);
				this.ViewData["Target-Properties"] = TypeDescriptor.GetProperties(this.ViewData["Target"]);
			}

			return this.View(node);
		}

		public JsonResult Find(string path)
		{
			if(string.IsNullOrWhiteSpace(path))
				return null;

			var result = _pluginContext.ResolvePath(path);
			return new JsonResult(result);
		}
		#endregion

		#region 嵌套子类
		public class JsonResult : ActionResult
		{
			#region 成员字段
			private object _data;
			#endregion

			#region 构造函数
			public JsonResult(object data)
			{
				if(data == null)
					throw new ArgumentNullException("data");

				_data = data;
			}
			#endregion

			#region 重写方法
			public override void ExecuteResult(ControllerContext context)
			{
				var response = context.HttpContext.Response;

				response.ContentType = "application/json";
				response.ContentEncoding = Encoding.UTF8;

				using(var stream = new MemoryStream())
				{
					Zongsoft.Runtime.Serialization.Serializer.Json.Serialize(stream, _data);
					stream.Position = 0;

					using(var reader = new StreamReader(stream))
					{
						response.Write(reader.ReadToEnd());
					}
				}
			}
			#endregion
		}
		#endregion
	}
}
