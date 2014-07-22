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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Mvc;
using System.Web.Compilation;

using Zongsoft.Plugins;

namespace Zongsoft.Web.Plugins.Mvc
{
	public class PluginWebFormView : System.Web.Mvc.IView
	{
		#region 成员变量
		private string _viewPath;
		private string _masterPath;
		private PluginContext _pluginContext;
		#endregion

		#region 构造函数
		public PluginWebFormView(PluginContext pluginContext, string viewPath) : this(pluginContext, viewPath, null)
		{
		}

		public PluginWebFormView(PluginContext pluginContext, string viewPath, string masterPath)
		{
			if(pluginContext == null)
				throw new ArgumentNullException("pluginContext");

			if(string.IsNullOrEmpty(viewPath))
				throw new ArgumentNullException("viewPath");

			_pluginContext = pluginContext;
			_viewPath = viewPath;
			_masterPath = masterPath ?? string.Empty;
		}
		#endregion

		#region 公共属性
		public PluginContext PluginContext
		{
			get
			{
				return _pluginContext;
			}
		}

		public string MasterPath
		{
			get
			{
				return _masterPath;
			}
		}

		public string ViewPath
		{
			get
			{
				return _viewPath;
			}
		}
		#endregion

		#region 生成视图
		public void Render(ViewContext viewContext, TextWriter writer)
		{
			if(viewContext == null)
				throw new ArgumentNullException("viewContext");

			object viewInstance = BuildManager.CreateInstanceFromVirtualPath(this.ViewPath, typeof(object));
			if(viewInstance == null)
				throw new InvalidOperationException(string.Format("The view found at '{0}' was not created.", this.ViewPath));

			ViewPage viewPage = viewInstance as ViewPage;
			if(viewPage != null)
			{
				RenderViewPage(viewContext, viewPage);
				return;
			}

			ViewUserControl viewUserControl = viewInstance as ViewUserControl;
			if(viewUserControl != null)
			{
				RenderViewUserControl(viewContext, viewUserControl);
				return;
			}

			throw new InvalidOperationException(
				string.Format(
					"The view at '{0}' must derive from ViewPage, ViewPage<TViewData>, ViewUserControl, or ViewUserControl<TViewData>.",
					this.ViewPath));
		}
		#endregion

		#region 生成方法
		private void RenderViewPage(ViewContext context, ViewPage page)
		{
			if(!string.IsNullOrEmpty(this.MasterPath))
				page.MasterLocation = this.MasterPath;

			page.Init += delegate
			{
				//动态添加页面内容
				this.GeneratePageContent(context, page);
			};

			page.ViewData = context.ViewData;
			page.RenderView(context);
		}

		private void RenderViewUserControl(ViewContext context, ViewUserControl control)
		{
			if(!string.IsNullOrEmpty(this.MasterPath))
				throw new InvalidOperationException("A master name cannot be specified when the view is a ViewUserControl.");

			control.ViewData = context.ViewData;
			control.RenderView(context);
		}
		#endregion

		#region 私有方法
		private void GeneratePageContent(ViewContext context, Page page)
		{
			string controllerName = context.RouteData.GetRequiredString("controller");
			string actionName = context.RouteData.GetRequiredString("action");
			string areaName = context.RouteData.DataTokens["area"] as string;

			//生成主题相关的内容
			Zongsoft.Web.Themes.ThemeUtility.GenerateTheme(page);

			page.Items["__themes__"] = Zongsoft.Web.Themes.ThemeUtility.GetThemeNames(page);
			page.Items["__theme__"] = Zongsoft.Web.Themes.ThemeUtility.GetCurrentThemeName(page);

			//首先生成工作台节点下的基本控件
			this.GeneratePageContent(page, _pluginContext.PluginTree.Find(_pluginContext.Settings.WorkbenchPath));

			string path = PluginPath.Combine(string.IsNullOrWhiteSpace(areaName) ? "Workspace" : areaName, "Views", controllerName, actionName);
			var viewNode = _pluginContext.PluginTree.Find(path);

			if(viewNode != null)
				this.GeneratePageContent(page, viewNode);
		}

		//注意：该方法不需要递归自调用，因为PluginTreeNode对象的Build方法会内部进行子节点的构建
		private void GeneratePageContent(Page page, PluginTreeNode ownerNode)
		{
			if(ownerNode == null)
				return;

			string place = string.Empty;

			foreach(PluginTreeNode childNode in ownerNode.Children)
			{
				if(childNode.NodeType == PluginTreeNodeType.Empty)
					continue;

				//只有当前节点的目标类型为控件时，才需要构建它们
				if(typeof(System.Web.UI.Control).IsAssignableFrom(childNode.ValueType))
				{
					place = childNode.Properties.GetValue<string>("place");

					//if(childNode.NodeType == PluginTreeNodeType.Builtin)
					//    place = ((Builtin)childNode.Value).Properties.GetValue<string>("place");

					Control placeControl = this.FindPlaceControl(page, place);
					if(placeControl != null)
						childNode.Build(placeControl);
				}
			}
		}

		/// <summary>
		/// 在页面中根据指定的位置描述查找特定的控件。
		/// </summary>
		/// <param name="page">要查找的页面对象。</param>
		/// <param name="place">要查找的位置描述字符串，该字符串由控件名称组成，上下级控件名中间由点(.)分隔。该参数为空字符串或空白字符则返回页面的Form或指定的页面对象。</param>
		/// <returns>返回查找到的控件，如果查找失败则返回空(null)。</returns>
		private Control FindPlaceControl(Page page, string place)
		{
			if(page == null)
				return null;

			if(string.IsNullOrWhiteSpace(place))
				return (Control)page.Form ?? page;

			Control placeControl = (Control)page.Master ?? page;
			string[] parts = place.Split('.');

			foreach(string part in parts)
			{
				if(string.IsNullOrWhiteSpace(part))
					throw new InvalidOperationException(string.Format("The value of place is invalid, it is '{0}'.", place));

				placeControl = placeControl.FindControl(part);
				if(placeControl == null)
					return null;
			}

			return placeControl;
		}
		#endregion
	}
}
