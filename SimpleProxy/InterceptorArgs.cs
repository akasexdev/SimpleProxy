namespace SimpleProxy
{
	public abstract class InterceptorArgs
	{
		public readonly MethodInvocation Invocation;
		public readonly object proxy;
		public object Result;

		protected InterceptorArgs(object proxy, MethodInvocation invocation)
		{
			this.proxy = proxy;
			Invocation = invocation;
		}

		public abstract void Proceed();
	}
}