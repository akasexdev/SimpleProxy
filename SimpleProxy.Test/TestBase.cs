using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SimpleProxy.Test
{
	[TestFixture]
	public class TestBase
	{
		public class WithoutTarget_MethodWithoutArgs
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
				var invocations = new List<MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo();
				Assert.That(invocations.Single().MethodInfo.Name, Is.EqualTo("Foo"));
				Assert.That(invocations.Single().Arguments, Is.Empty);
			}
		}

		public class MethodArgumentsPassedCorrectly: TestBase
		{
			public interface IFoo
			{
				void Foo(string one, string two);
			}

			[Test]
			public void Test()
			{
				var invocations = new List<MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IFoo>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo("one", "two");
				Assert.That(invocations.Single().MethodInfo, Is.EqualTo(typeof (IFoo).GetMethod("Foo")));
				Assert.That(invocations.Single().Arguments, Is.EqualTo(new object[] { "one", "two" }));
			}
		}

		public class PrimitiveArgsPassedCorrectly: TestBase
		{
			public interface IFoo
			{
				void Foo(int one, double two, decimal three);
			}

			[Test]
			public void Test()
			{
				var invocations = new List<MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IFoo>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo(1, 2.5, 3m);
				Assert.That(invocations.Single().MethodInfo.Name, Is.EqualTo("Foo"));
				Assert.That(invocations.Single().Arguments, Is.EqualTo(new object[] { 1, 2.5, 3m }));
			}
		}

		public class ParamsArgPassedAsSingleArg: TestBase
		{
			public interface IFoo
			{
				void Foo(params int[] values);
			}

			[Test]
			public void Test()
			{
				var invocations = new List<MethodInvocation>();
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IFoo>(new DelegateHandler(c => invocations.Add(c)));
				proxy.Foo(1, 2, 3);
				Assert.That(invocations.Single().MethodInfo.Name, Is.EqualTo("Foo"));
				Assert.That(invocations.Single().Arguments.Single(), Is.EqualTo(new object[] { 1, 2, 3 }));
			}
		}

		public class WithoutTarget_CanReturnValue: TestBase
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

		public class WithoutTarget_CanReturnPrimitiveValue: TestBase
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

		public class WithoutTarget_CanReturnUnderlyingNullable: TestBase
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

		public class WithoutTarget_CanImplementMultipleInterfaces: TestBase
		{
			public interface IOne
			{
				string One();
			}

			public interface ITwo
			{
				string Two();
			}

			[Test]
			public void Test()
			{
				var proxy = SimpleProxyFactory.CreateProxyWithoutTarget<IOne>(new DelegateHandler(c => c.MethodInfo.Name), typeof (ITwo));
				Assert.That(proxy.One(), Is.EqualTo("One"));
				Assert.That(((ITwo) proxy).Two(), Is.EqualTo("Two"));
			}
		}

		public class WithTarget_CalledWithCorrectParameters: TestBase
		{
			public interface IInterface
			{
				void Foo(int one, int two, int three, string four);
			}

			private class Impl: IInterface
			{
				public readonly List<object[]> Invocations = new List<object[]>();

				public void Foo(int one, int two, int three, string four)
				{
					Invocations.Add(new object[] { one, two, three, four });
				}
			}

			[Test]
			public void Test()
			{
				var impl = new Impl();
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(c => c.Proceed()), impl);
				proxy.Foo(1, 2, 3, "four");
				Assert.That(impl.Invocations.Single(), Is.EqualTo(new object[] { 1, 2, 3, "four" }));
			}
		}

		public class WithTarget_CanAddSimpleBehaviorForVoidMethod: TestBase
		{
			public interface IInterface
			{
				void Foo();
			}

			public class Impl: IInterface
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

		public class WithTarget_CanAlterArguments: TestBase
		{
			public interface IInterface
			{
				void Foo(int arg);
			}

			public class Impl: IInterface
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

		public class WithTarget_TestReturnValue: TestBase
		{
			public interface IInterface
			{
				string Foo();
			}

			public class Impl: IInterface
			{
				public string Foo()
				{
					return "42";
				}
			}

			[Test]
			public void CanInspectReturnValue()
			{
				var target = new Impl();
				string returnValue = "";
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(args =>
				{
					args.Proceed();
					returnValue = (string) args.Result;
				}), target);
				var actual = proxy.Foo();
				Assert.That(returnValue, Is.EqualTo("42"));
				Assert.That(actual, Is.EqualTo("42"));
			}

			[Test]
			public void CanAlterReturnValue()
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

		public class WithTarget_ArgumentOfWrongType_ThrowException
		{
			public interface IInterface
			{
				void Foo(string one, int two);
			}

			public class Impl: IInterface
			{
				public void Foo(string one, int two)
				{
				}
			}

			[Test]
			public void Test()
			{
				var proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(a =>
																										{
																											a.Invocation.Arguments[0] = 123;
																											a.Proceed();
																										}), new Impl());
				Assert.Throws<InvalidCastException>(() => proxy.Foo("one", 2));
				proxy = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(a =>
																									{
																										a.Invocation.Arguments[1] = "123";
																										a.Proceed();
																									}), new Impl());
				Assert.Throws<InvalidCastException>(() => proxy.Foo("one", 2));
			}
		}


		public class WithTarget_CanProxyMultipleInterfaces
		{
			public interface IOne
			{
				string One();
			}

			public interface ITwo
			{
				string Two();
			}

			public class Impl:IOne, ITwo
			{
				public string One()
				{
					return "one";
				}

				public string Two()
				{
					return "two";
				}
			}

			[Test]
			public void Test()
			{
				var proxy = SimpleProxyFactory.CreateProxyForTarget<ITwo>(new DelegateInterceptor(args =>
																								  {
																									  args.Proceed();
																								  }), new Impl(), typeof (IOne));
				Assert.That(proxy.Two(), Is.EqualTo("two"));
				Assert.That(((IOne) proxy).One(), Is.EqualTo("one"));
			}
		}

		public class CanCreateProxyForClosedGenericInterface: TestBase
		{
			public interface IGenericInterface<T>
			{
				T Foo(T value);
			}

			public class Impl<T>: IGenericInterface<T>
			{
				public T Foo(T value)
				{
					return value;
				}
			}

			[Test]
			public void CreateProxyForClosedInterface()
			{
				var intProxy = SimpleProxyFactory.CreateProxyForTarget<IGenericInterface<int>>(new DelegateInterceptor(args =>
																													{
																														args.Invocation.Arguments[0] = (int) args.Invocation.Arguments[0] + 1;
																														args.Proceed();
																														args.Result = (int) args.Result + 1;
																													}), new Impl<int>());
				var stringProxy = SimpleProxyFactory.CreateProxyForTarget<IGenericInterface<string>>(new DelegateInterceptor(args => args.Proceed()), new Impl<string>());
				Assert.That(intProxy.Foo(42), Is.EqualTo(44));
				Assert.That(stringProxy.Foo("foo"), Is.EqualTo("foo"));
			}
		}

		public class CanCreateDifferentProxiesForSameType: TestBase
		{
			public interface IInterface
			{
				string Foo();
			}

			public interface IAnotherInterface
			{
				string Bar();
			}

			private class Impl: IInterface
			{
				private readonly string value;

				public Impl(string value)
				{
					this.value = value;
				}

				public string Foo()
				{
					return value;
				}
			}

			[Test]
			public void Test()
			{
				var withoutTarget1 = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new DelegateHandler(_ => "withoutTarget1"));
				var withoutTarget2 = SimpleProxyFactory.CreateProxyWithoutTarget<IInterface>(new DelegateHandler(_ => "withoutTarget2"));
				var withTarget1 = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(args => args.Result = "newValue1"), new Impl("originalValue1"));
				var withTarget2 = SimpleProxyFactory.CreateProxyForTarget<IInterface>(new DelegateInterceptor(args => args.Result = "newValue2"), new Impl("originalValue2"));

				Assert.That(withoutTarget1.Foo(), Is.EqualTo("withoutTarget1"));
				Assert.That(withoutTarget2.Foo(), Is.EqualTo("withoutTarget2"));
				Assert.That(withTarget1.Foo(), Is.EqualTo("newValue1"));
				Assert.That(withTarget2.Foo(), Is.EqualTo("newValue2"));
			}
		}

		public class DelegateHandler: IHandler
		{
			private readonly Func<MethodInvocation, object> handle;

			public DelegateHandler(Func<MethodInvocation, object> handle)
			{
				this.handle = handle;
			}

			public DelegateHandler(Action<MethodInvocation> handle)
			{
				this.handle = i =>
							  {
								  handle(i);
								  return null;
							  };
			}

			public object Handle(MethodInvocation invocation)
			{
				return handle(invocation);
			}
		}

		public class DelegateInterceptor: IInterceptor
		{
			private readonly Action<InterceptorArgs> action;

			public DelegateInterceptor(Action<InterceptorArgs> action)
			{
				this.action = action;
			}

			public void Handle(InterceptorArgs args)
			{
				action(args);
			}
		}
	}
}