using System.Reflection;

namespace SimpleProxy
{
	public struct MethodInvocation
	{
		private static readonly object[] emptyArgs = new object[0];
		public readonly object[] Arguments;
		public readonly MethodInfo MethodInfo;

		public MethodInvocation(MethodInfo method, object[] args)
		{
			Arguments = args ?? emptyArgs;
			MethodInfo = method;
		}
	}
}