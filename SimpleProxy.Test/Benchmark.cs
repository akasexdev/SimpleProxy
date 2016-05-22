using BenchmarkDotNet.Attributes;
using Castle.DynamicProxy;
using Kontur.Elba.Core.Utilities.Reflection;

namespace Kontur.Elba.Utilities.Tests.Reflection
{
	public class Benchmark
	{
		public interface IInterface
		{
			void Foo();
			void FooWithParams(int one, int two, int three, int four);
		}

		private readonly IInterface castle;
		private readonly IInterface simple;

		public Benchmark()
		{
			castle = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IInterface>(new Interceptor());
			simple = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new Interceptor());
		}

		[Benchmark]
		public void Castle_NoParams()
		{
			castle.Foo();
		}

		[Benchmark]
		public void Simple_NoParams()
		{
			simple.Foo();
		}

		/*[Benchmark]
		public void Castle_Boxing()
		{
			castle.FooWithParams(1,2,3,4);
		}

		[Benchmark]
		public void Simple_Boxing()
		{
			simple.FooWithParams(1, 2, 3, 4);
		}*/
	}

	public class Interceptor : IInterceptor, SimpleProxyFactory.IHandler
	{
		public void Intercept(IInvocation invocation)
		{
		}

		public object Handle(SimpleProxyFactory.MethodInvocation invocation)
		{
			return null;
		}
	}
}