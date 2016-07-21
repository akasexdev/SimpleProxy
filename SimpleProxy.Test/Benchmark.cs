using BenchmarkDotNet.Attributes;
using Castle.DynamicProxy;

namespace SimpleProxy.Test
{
	public class Benchmark
	{
		private readonly IInterface castleForTarget;
		private readonly IInterface castleWithoutTarget;
		private readonly IInterface manualForTarget;
		private readonly IInterface manualWithoutTarget;
		private readonly IInterface simpleForTarget;
		private readonly IInterface simpleWithoutTarget;
		private readonly SomeType someTypeObj = new SomeType();

		public Benchmark()
		{
			manualWithoutTarget = new Impl();
			manualForTarget = new Decorator(new Impl());
			castleForTarget = new ProxyGenerator().CreateInterfaceProxyWithTarget<IInterface>(new Impl(), new CastleInterceptor(true));
			simpleForTarget = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new SimpleInterceptor(), new Impl());
			castleWithoutTarget = new ProxyGenerator().CreateInterfaceProxyWithoutTarget<IInterface>(new CastleInterceptor(false));
			simpleWithoutTarget = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new SimpleInterceptor());
		}


		[Benchmark]
		public void Manual_NoParams()
		{
			manualForTarget.NoParameters();
		}

		[Benchmark]
		public void Castle_NoParams()
		{
			castleForTarget.NoParameters();
		}

		[Benchmark]
		public void Simple_NoParams()
		{
			simpleForTarget.NoParameters();
		}
		
		[Benchmark]
		public void Manual_ForTarget_ManyValueParams()
		{
			manualForTarget.MultipleValueParameters(1, 2, 3, 4);
		}

		[Benchmark]
		public void Castle_ForTarget_ManyValueParams()
		{
			castleForTarget.MultipleValueParameters(1, 2, 3, 4);
		}
		
		[Benchmark]
		public void Simple_ForTarget_ManyValueParams()
		{
			simpleForTarget.MultipleValueParameters(1, 2, 3, 4);
		}
		
		[Benchmark]
		public void Manual_ForTarget_MultipleReferenceParams()
		{
			manualForTarget.MultipleReferenceParameters(someTypeObj, someTypeObj, someTypeObj, someTypeObj);
		}

		[Benchmark]
		public void Castle_ForTarget_MultipleReferenceParams()
		{
			castleForTarget.MultipleReferenceParameters(someTypeObj, someTypeObj, someTypeObj, someTypeObj);
		}

		[Benchmark]
		public void Simple_ForTarget_MultipleReferenceParams()
		{
			simpleForTarget.MultipleReferenceParameters(someTypeObj, someTypeObj, someTypeObj, someTypeObj);
		}

		//without target
		[Benchmark]
		public void Manual_WithoutTarget_NoParameters()
		{
			manualWithoutTarget.NoParameters();
		}

		[Benchmark]
		public void Castle_WithoutTarget_NoParameters()
		{
			castleWithoutTarget.NoParameters();
		}

		[Benchmark]
		public void Simple_WithoutTarget_NoParameters()
		{
			simpleWithoutTarget.NoParameters();
		}

		[Benchmark]
		public void Manual_WithoutTarget_ManyValueParams()
		{
			manualWithoutTarget.MultipleValueParameters(1, 2, 3, 4);
		}

		[Benchmark]
		public void Castle_WithoutTarget_ManyValueParams()
		{
			castleWithoutTarget.MultipleValueParameters(1, 2, 3, 4);
		}

		[Benchmark]
		public void Simple_WithoutTarget_ManyValueParams()
		{
			simpleWithoutTarget.MultipleValueParameters(1, 2, 3, 4);
		}

		[Benchmark]
		public void Manual_WithoutTarget_MultipleReferenceParams()
		{
			manualWithoutTarget.MultipleReferenceParameters(someTypeObj, someTypeObj, someTypeObj, someTypeObj);
		}

		[Benchmark]
		public void Castle_WithoutTarget_MultipleReferenceParams()
		{
			castleWithoutTarget.MultipleReferenceParameters(someTypeObj, someTypeObj, someTypeObj, someTypeObj);
		}

		[Benchmark]
		public void Simple_WithoutTarget_MultipleReferenceParams()
		{
			simpleWithoutTarget.MultipleReferenceParameters(someTypeObj, someTypeObj, someTypeObj, someTypeObj);
		}

		public interface IInterface
		{
			void NoParameters();
			void MultipleValueParameters(int one, int two, int three, int four);
			void MultipleReferenceParameters(SomeType one, SomeType two, SomeType three, SomeType four);
		}

		private class Impl : IInterface
		{
			public void NoParameters()
			{
			}

			public void MultipleValueParameters(int one, int two, int three, int four)
			{
			}

			public void MultipleReferenceParameters(SomeType one, SomeType two, SomeType three, SomeType four)
			{
				
			}
		}

		public class CastleInterceptor: Castle.DynamicProxy.IInterceptor
		{
			private readonly bool forTarget;

			public CastleInterceptor(bool forTarget)
			{
				this.forTarget = forTarget;
			}

			public void Intercept(IInvocation invocation)
			{
				if (forTarget)
					invocation.Proceed();
			}
		}

		public class SimpleInterceptor : IInterceptor, IHandler
		{
			public void Handle(InterceptorArgs args)
			{
				args.Proceed();
			}

			public object Handle(MethodInvocation invocation)
			{
				return null;
			}
		}

		public class Decorator : IInterface
		{
			private readonly IInterface target;

			public Decorator(IInterface target)
			{
				this.target = target;
			}

			public void NoParameters()
			{
				target.NoParameters();
			}

			public void MultipleValueParameters(int one, int two, int three, int four)
			{
				target.MultipleValueParameters(one, two, three, four);
			}

			public void MultipleReferenceParameters(SomeType one, SomeType two, SomeType three, SomeType four)
			{
				target.MultipleReferenceParameters(one, two, three, four);
			}
		}

		public class SomeType
		{
			
		}
	}
}