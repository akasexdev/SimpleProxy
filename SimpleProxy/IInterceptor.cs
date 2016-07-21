namespace SimpleProxy
{
	public interface IInterceptor
	{
		void Handle(InterceptorArgs args);
	}
}