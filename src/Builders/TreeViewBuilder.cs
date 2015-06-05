﻿/*
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
using System.Web.UI;

using Zongsoft.Plugins;
using Zongsoft.Plugins.Builders;

namespace Zongsoft.Web.Plugins.Builders
{
	[BuilderBehaviour(IsPreserveValue = false, ValueType = typeof(Zongsoft.Web.Controls.TreeView))]
	public class TreeViewBuilder : ControlBuilder
	{
		#region 重写方法
		public override object Build(BuilderContext context)
		{
			var treeView = new Zongsoft.Web.Controls.TreeView()
			{
				ID = context.Builtin.Name,
				CssClass = context.Builtin.Properties.GetValue<string>("cssClass"),
				DataSource = context.Builtin.Properties.GetValue("dataSource"),
				IsDropdown = context.Builtin.Properties.GetValue<bool>("isDropdown"),
				RenderMode = context.Builtin.Properties.GetValue<Zongsoft.Web.Controls.ListRenderMode>("renderMode", Zongsoft.Web.Controls.ListRenderMode.List),
				Height = context.Builtin.Properties.GetValue<Zongsoft.Web.Controls.Unit>("height", Zongsoft.Web.Controls.Unit.Empty),
				Width = context.Builtin.Properties.GetValue<Zongsoft.Web.Controls.Unit>("width", Zongsoft.Web.Controls.Unit.Empty),
				ScrollbarMode = context.Builtin.Properties.GetValue<Zongsoft.Web.Controls.ScrollbarMode>("scrollbarMode", Zongsoft.Web.Controls.ScrollbarMode.None),
				SelectionMode = context.Builtin.Properties.GetValue<Zongsoft.Web.Controls.SelectionMode>("selectionMode", Zongsoft.Web.Controls.SelectionMode.None),
				Visible = context.Builtin.Properties.GetValue<bool>("visible", true),
			};

			return treeView;
		}
		#endregion
	}
}
