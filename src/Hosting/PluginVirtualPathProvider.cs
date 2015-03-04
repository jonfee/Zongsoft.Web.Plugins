/*
 * Authors:
 *   钟峰(Popeye Zhong) <zongsoft@gmail.com>
 *
 * Copyright (C) 2014 Zongsoft Corporation <http://www.zongsoft.com>
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
using System.Web.Hosting;

using Zongsoft.Plugins;

namespace Zongsoft.Web.Plugins.Hosting
{
	public class PluginVirtualPathProvider : System.Web.Hosting.VirtualPathProvider
	{
		#region 成员字段
		private PluginContext _pluginContext;
		#endregion

		#region 构造函数
		public PluginVirtualPathProvider(PluginContext pluginContext)
		{
			if(pluginContext == null)
				throw new ArgumentNullException("pluginContext");

			_pluginContext = pluginContext;
		}
		#endregion

		public override bool FileExists(string virtualPath)
		{
			var exists = base.FileExists(virtualPath);
			return exists;
		}

		public override VirtualDirectory GetDirectory(string virtualDir)
		{
			return base.GetDirectory(virtualDir);
		}

		public override VirtualFile GetFile(string virtualPath)
		{
			if(string.IsNullOrWhiteSpace(virtualPath))
				return null;

			return base.GetFile(virtualPath);
		}
	}
}
