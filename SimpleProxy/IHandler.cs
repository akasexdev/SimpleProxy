namespace SimpleProxy
{
	public interface IHandler
	{
		object Handle(MethodInvocation invocation);
	}
}