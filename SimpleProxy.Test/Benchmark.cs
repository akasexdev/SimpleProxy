using BenchmarkDotNet.Attributes;
using Castle.DynamicProxy;
using Kontur.Elba.Core.Utilities.Reflection;

namespace Kontur.Elba.Utilities.Tests.Reflection
{
	public class Benchmark
	{
		private readonly IInterface castleForTarget;
		private readonly IInterface castleWithoutTarget;
		private readonly IInterface manualForTarget;
		private readonly IInterface manualWithoutTarget;
		private readonly IInterface simpleForTarget;
		private readonly IInterface simpleWithoutTarget;

		public Benchmark()
		{
			manualWithoutTarget = new Impl();
			manualForTarget = new Decorator(new Impl());
			castleForTarget = new ProxyGenerator().CreateInterfaceProxyWithTarget<IInterface>(new Impl(), new Interceptor());
			simpleForTarget = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new Interceptor(), new Impl());
			castleWithoutTarget = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IInterface>(new Interceptor());
			simpleWithoutTarget = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new Interceptor());
		}

		[Benchmark]
		public void Manual_NoParams()
		{
			manualForTarget.Foo();
		}

		[Benchmark]
		public void Castle_NoParams()
		{
			castleForTarget.Foo();
		}

		[Benchmark]
		public void Simple_NoParams()
		{
			simpleForTarget.Foo();
		}
		
		/*[Benchmark]
		public void Manual_ManySimpleParams()
		{
			manualForTarget.FooWithParams(1, 2, 3, 4);
		}

		[Benchmark]
		public void Castle_ManySimpleParams()
		{
			castleForTarget.FooWithParams(1, 2, 3, 4);
		}

		[Benchmark]
		public void Simple_ManySimpleParams()
		{
			simpleForTarget.FooWithParams(1, 2, 3, 4);
		}*/
		/*
		[Benchmark]
		public void Manual_WithoutTarget_NoParameters()
		{
			manualWithoutTarget.Foo();
		}

		[Benchmark]
		public void Castle_WithoutTarget_NoParameters()
		{
			castleWithoutTarget.Foo();
		}

		[Benchmark]
		public void Simple_WithoutTarget_NoParameters()
		{
			simpleWithoutTarget.Foo();
		}*/

		/*[Benchmark]
		public void Manual_WithoutTarget_ManySimpleParams()
		{
			manualWithoutTarget.FooWithParams(1, 2, 3, 4);
		}

		[Benchmark]
		public void Castle_WithoutTarget_ManySimpleParams()
		{
			castleWithoutTarget.FooWithParams(1, 2, 3, 4);
		}

		[Benchmark]
		public void Simple_WithoutTarget_ManySimpleParams()
		{
			simpleWithoutTarget.FooWithParams(1, 2, 3, 4);
		}*/

		public interface IInterface
		{
			void Foo();
			void FooWithParams(int one, int two, int three, int four);
		}

		private class Impl : IInterface
		{
			public void Foo()
			{
			}

			public void FooWithParams(int one, int two, int three, int four)
			{
			}
		}

		public class Interceptor : IInterceptor, SimpleProxyFactory.IHandler, SimpleProxyFactory.IInterceptor
		{
			public object Handle(SimpleProxyFactory.MethodInvocation invocation)
			{
				return null;
			}

			public void Intercept(IInvocation invocation)
			{
			}

			public void Handle(SimpleProxyFactory.InterceptorArgs args)
			{
			}
		}

		public class Decorator : IInterface
		{
			private readonly IInterface target;

			public Decorator(IInterface target)
			{
				this.target = target;
			}

			public void Foo()
			{
				target.Foo();
			}

			public void FooWithParams(int one, int two, int three, int four)
			{
				target.FooWithParams(one, two, three, four);
			}
		}
	}
}