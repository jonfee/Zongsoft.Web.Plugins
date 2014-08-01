using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Collections.Generic
{
	internal static class CollectionExtension
	{
		public static bool TryGetValue<T>(this IDictionary<string, object> collection, string key, out T value)
		{
			System.Diagnostics.Contracts.Contract.Assert(collection != null);

			object valueObj;
			if(collection.TryGetValue(key, out valueObj))
			{
				if(valueObj is T)
				{
					value = (T)valueObj;
					return true;
				}
			}

			value = default(T);
			return false;
		}
	}
}
