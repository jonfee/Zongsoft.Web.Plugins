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
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Zongsoft.Web.Plugins
{
	public class Workbench : Zongsoft.Plugins.WorkbenchBase
	{
		#region 构造函数
		internal Workbench(ApplicationContext applicationContext) : base(applicationContext)
		{
			//添加插件视图引擎到视图引擎集合中
			this.ViewEngines.Insert(0, new Zongsoft.Web.Plugins.Mvc.PluginWebFormViewEngine(this.PluginContext));
			//替换系统默认控制器工厂
			this.ControllerFactory = new Zongsoft.Web.Plugins.Mvc.PluginControllerFactory(this.PluginContext);
		}
		#endregion

		#region 公共属性
		public IControllerFactory ControllerFactory
		{
			get
			{
				return System.Web.Mvc.ControllerBuilder.Current.GetControllerFactory();
			}
			set
			{
				if(value == null)
					throw new ArgumentNullException();

				System.Web.Mvc.ControllerBuilder.Current.SetControllerFactory(value);
			}
		}

		public ViewEngineCollection ViewEngines
		{
			get
			{
				return System.Web.Mvc.ViewEngines.Engines;
			}
		}
		#endregion

		#region 重写方法
		protected override void OnStart(string[] args)
		{
			//初始化MVC的环境
			System.Web.Mvc.FilterProviders.Providers.Add(new Zongsoft.Web.Plugins.Mvc.PluginFilterProvider(this.PluginContext));

			//替换系统默认的服务
			System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Dispatcher.IHttpControllerSelector), new Http.PluginHttpControllerSelector(this.PluginContext));
			System.Web.Http.GlobalConfiguration.Configuration.Services.Replace(typeof(System.Web.Http.Controllers.IHttpActionSelector), new Http.HttpControllerActionSelector());

			//更改序列化器的默认设置
			System.Web.Http.GlobalConfiguration.Configuration.Formatters.XmlFormatter.UseXmlSerializer = true;
			var contractResolver = System.Web.Http.GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ContractResolver as Newtonsoft.Json.Serialization.DefaultContractResolver;
			if(contractResolver != null)
				contractResolver.IgnoreSerializableAttribute = true;

			//调用基类同名方法，以启动工作台下Startup下的所有工作者
			base.OnStart(args);
		}
		#endregion
	}
}
