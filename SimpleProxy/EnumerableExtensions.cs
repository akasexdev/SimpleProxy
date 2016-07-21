using System.Collections.Generic;
using System.Linq;

namespace SimpleProxy
{
	public static class EnumerableExtensions
	{
		public static string JoinStrings<T>(this IEnumerable<T> values, string separator = "")
		{
			return string.Join(separator, values.Select(x => x.ToString()).ToArray());
		}

		public static bool IsEquivalentTo<T>(this IEnumerable<T> value, IEnumerable<T> other)
		{
			return new HashSet<T>(value).SetEquals(other);
		}
	}
}