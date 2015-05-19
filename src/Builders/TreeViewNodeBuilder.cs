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

using Zongsoft.Plugins;
using Zongsoft.Plugins.Builders;

namespace Zongsoft.Web.Plugins.Builders
{
	[BuilderBehaviour(IsPreserveValue = false, ValueType = typeof(Zongsoft.Web.Controls.TreeViewNode))]
	public class TreeViewNodeBuilder : BuilderBase
	{
		#region 重写方法
		public override object Build(BuilderContext context)
		{
			Builtin builtin = context.Builtin;

			var node = new Zongsoft.Web.Controls.TreeViewNode(builtin.Name, builtin.Properties.GetValue<string>("text"))
			{
				Url = builtin.Properties.GetValue<string>("url") ?? string.Empty,
				Icon = builtin.Properties.GetValue<string>("icon"),
				Text = builtin.Properties.GetValue<string>("text", builtin.Name),
				ToolTip = builtin.Properties.GetValue<string>("tooltip"),
				Description = builtin.Properties.GetValue<string>("description"),
				Selected = builtin.Properties.GetValue<bool>("selected", false),
				Visible = builtin.Properties.GetValue<bool>("visible", true),
			};

			//返回构建的目标对象
			return node;
		}

		protected override void OnBuilt(BuilderContext context)
		{
			if(context.Owner == null)
				return;

			var node = context.Result as Zongsoft.Web.Controls.TreeViewNode;

			if(node == null)
				return;

			//根据所有者对象的类型，将当前目标对象添加到其子项列表中
			if(context.Owner is Zongsoft.Web.Controls.TreeViewNode)
			{
				((Zongsoft.Web.Controls.TreeViewNode)context.Owner).Nodes.Add(node);
			}
			else if(context.Owner is Zongsoft.Web.Controls.TreeView)
			{
				((Zongsoft.Web.Controls.TreeView)context.Owner).Nodes.Add(node);
			}
		}
		#endregion
	}
}
