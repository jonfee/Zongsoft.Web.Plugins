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
using System.Web;
using System.Collections.Generic;

using Zongsoft.Plugins;

namespace Zongsoft.Web.Plugins
{
	public class ApplicationContext : Zongsoft.Plugins.PluginApplicationContext
	{
		#region 单例字段
		public static readonly ApplicationContext Current = new ApplicationContext();
		#endregion

		#region 私有构造
		private ApplicationContext() : base("Zongsoft.Web.Plugins")
		{
			string filePaht = Path.Combine(this.ApplicationDirectory, "Web.option");

			if(File.Exists(filePaht))
				this.Configuration = Options.Configuration.OptionConfiguration.Load(filePaht);
		}
		#endregion

		#region 重写方法
		public override string ApplicationDirectory
		{
			get
			{
				return HttpContext.Current.Server.MapPath("~");
			}
		}

		public override System.Security.Principal.IPrincipal Principal
		{
			get
			{
				return HttpContext.Current.User;
			}
			set
			{
				HttpContext.Current.User = value;
			}
		}

		protected override IWorkbenchBase CreateWorkbench(string[] args)
		{
			PluginTreeNode node = this.PluginContext.PluginTree.Find(this.PluginContext.Settings.WorkbenchPath);

			if(node != null && node.NodeType == PluginTreeNodeType.Builtin)
				return base.CreateWorkbench(args);

			return new Workbench(this);
		}
		#endregion
	}
}
