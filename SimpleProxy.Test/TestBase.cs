using System;
using System.Collections.Generic;
using System.Linq;
using Kontur.Elba.Core.Utilities.Reflection;
using NUnit.Framework;

namespace Kontur.Elba.Utilities.Tests.Reflection
{
	[TestFixture]
	public class TestBase
	{
		public class InvokeMethodWithoutArgs
		{
			public interface IInterface2
			{

			}
			public interface IInterface
			{
				void Foo();
			}

			[Test]
			public void Test()
			{
				var invocations = new List<SimpleProxyFactory.MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new DelegateHandler(c => invocations.Add(c)));
				var proxy2 = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface2>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo();
				Assert.That(invocations.Single().MethodInfo.Name, Is.EqualTo("Foo"));
				Assert.That(invocations.Single().Arguments, Is.Empty);
			}
		}

		public class MethodWithArgs : TestBase
		{
			public interface IFoo
			{
				void Foo(string one, string two);
			}

			[Test]
			public void Test()
			{
				var invocations = new List<SimpleProxyFactory.MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IFoo>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo("one", "two");
				Assert.That(invocations.Single().MethodInfo, Is.EqualTo(typeof(IFoo).GetMethod("Foo")));
				Assert.That(invocations.Single().Arguments, Is.EqualTo(new object[] { "one", "two" }));
			}
		}

		public class MethodWithPrimitiveArgs : TestBase
		{
			public interface IFoo
			{
				void Foo(int one, double two, decimal three);
			}

			[Test]
			public void Test()
			{
				var invocations = new List<SimpleProxyFactory.MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IFoo>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo(1, 2.5, 3m);
				Assert.That(invocations.Single().MethodInfo.Name, Is.EqualTo("Foo"));
				Assert.That(invocations.Single().Arguments, Is.EqualTo(new object[] { 1, 2.5, 3m }));
			}
		}

		public class MethodWithOptionalArgs : TestBase
		{
			public interface IFoo
			{
				void Foo(int one = 1, double two = 2, decimal three = 3);
			}

			[Test]
			public void Test()
			{
				var invocations = new List<SimpleProxyFactory.MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IFoo>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo();
				Assert.That(invocations.Single().MethodInfo.Name, Is.EqualTo("Foo"));
				Assert.That(invocations.Single().Arguments, Is.EqualTo(new object[] { 1, 2d, 3m }));
			}
		}

		public class MethodWithParams : TestBase
		{
			public interface IFoo
			{
				void Foo(params int[] values);
			}

			[Test]
			public void Test()
			{
				var invocations = new List<SimpleProxyFactory.MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IFoo>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo(1, 2, 3);
				Assert.That(invocations.Single().MethodInfo.Name, Is.EqualTo("Foo"));
				Assert.That(invocations.Single().Arguments.Single(), Is.EqualTo(new object[] { 1, 2, 3 }));
			}
		}

		public class CanReturnValue : TestBase
		{
			public interface IInterface
			{
				string Foo();
			}

			[Test]
			public void Test()
			{
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new DelegateHandler(c => "42"));
				Assert.That(proxy.Foo(), Is.EqualTo("42"));
			}
		}

		public class CanReturnPrimitiveValue : TestBase
		{
			public interface IInterface
			{
				int Foo();
			}

			[Test]
			public void Test()
			{
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new DelegateHandler(c => 42));
				Assert.That(proxy.Foo(), Is.EqualTo(42));
			}
		}

		public class CanReturnUnderlyingNullable : TestBase
		{
			public interface IInterface
			{
				int? Foo();
			}

			[Test]
			public void Test()
			{
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new DelegateHandler(c => 42));
				Assert.That(proxy.Foo(), Is.EqualTo(42));
			}
		}

		public class ProxyWithTarget_CanAddSimpleBehaviorForVoidMethod : TestBase
		{
			public interface IInterface
			{
				void Foo();
			}

			public class Impl : IInterface
			{
				public int Invocations;

				public void Foo()
				{
					Invocations++;
				}
			}

			[Test]
			public void Test()
			{
				var target = new Impl();
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(args =>
				{
					Console.WriteLine("invocation");
					args.Proceed();
				}), target);
				proxy.Foo();
				Assert.That(target.Invocations, Is.EqualTo(1));
			}
		}

		public class ProxyWithTarget_CanAlterArguments : TestBase
		{
			public interface IInterface
			{
				void Foo(int arg);
			}

			public class Impl : IInterface
			{
				public List<int> Invocations = new List<int>();

				public void Foo(int arg)
				{
					Invocations.Add(arg);
				}
			}

			[Test]
			public void Test()
			{
				var target = new Impl();
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(args =>
				{
					args.Invocation.Arguments[0] = 43;
					args.Proceed();
				}), target);
				proxy.Foo(42);
				Assert.That(target.Invocations.Single(), Is.EqualTo(43));
			}
		}

		public class ProxyWithTarget_CanAlterReturnValue : TestBase
		{
			public interface IInterface
			{
				string Foo();
			}

			public class Impl : IInterface
			{
				public string Foo()
				{
					return "42";
				}
			}

			[Test]
			public void Test()
			{
				var target = new Impl();
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(args =>
				{
					args.Proceed();
					args.Result = "43";
				}), target);
				var actual = proxy.Foo();
				Assert.That(actual, Is.EqualTo("43"));
			}

			[Test]
			public void ReturnValueOfWrongType_ThrowException()
			{
				var target = new Impl();
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(args =>
				{
					args.Proceed();
					args.Result = new object();
				}), target);
				Assert.Throws<InvalidCastException>(() => proxy.Foo());
			}
		}

		public class CreateProxyForGenericInterface : TestBase
		{
			public interface IGenericInterface<T>
			{
				T Foo(T value);
			}

			public class ClosedImpl : IGenericInterface<int>
			{
				public int Foo(int value)
				{
					return value;
				}
			}

			[Test]
			public void CreateProxyForClosedInterface()
			{
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IGenericInterface<int>>(new DelegateInterceptor(args =>
				{
					args.Invocation.Arguments[0] = (int)args.Invocation.Arguments[0] + 1;
					args.Proceed();
					args.Result = (int)args.Result + 1;
				}), new ClosedImpl());
				Assert.That(proxy.Foo(42), Is.EqualTo(44));
			}
		}

		public class DelegateHandler : SimpleProxyFactory.IHandler
		{
			private readonly Func<SimpleProxyFactory.MethodInvocation, object> handle;

			public DelegateHandler(Func<SimpleProxyFactory.MethodInvocation, object> handle)
			{
				this.handle = handle;
			}

			public DelegateHandler(Action<SimpleProxyFactory.MethodInvocation> handle)
			{
				this.handle = i =>
				{
					handle(i);
					return null;
				};
			}

			public object Handle(SimpleProxyFactory.MethodInvocation invocation)
			{
				return handle(invocation);
			}
		}

		public class DelegateInterceptor : SimpleProxyFactory.IInterceptor
		{
			private readonly Action<SimpleProxyFactory.InterceptorArgs> action;

			public DelegateInterceptor(Action<SimpleProxyFactory.InterceptorArgs> action)
			{
				this.action = action;
			}

			public void Handle(SimpleProxyFactory.InterceptorArgs args)
			{
				action(args);
			}
		}
	}
}