using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Kontur.Elba.Core.Utilities.Reflection
{
	public static class SimpleProxyFactory
	{
		private static int counter;
		private static readonly ModuleBuilder module;

		private static readonly ConcurrentDictionary<MethodInfo, Type> interceptorArgsTypes =
			new ConcurrentDictionary<MethodInfo, Type>();

		private static readonly ConcurrentDictionary<Type, Type> types =
			new ConcurrentDictionary<Type, Type>();

		static SimpleProxyFactory()
		{
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(typeof (SimpleProxyFactory).Name),
				AssemblyBuilderAccess.RunAndCollect);
			module = assemblyBuilder.DefineDynamicModule("main");
		}

		public static TInterface CreateProxyWithoutTarget<TInterface>(IHandler handler)
		{
			return CreateProxyForTarget(new Adapter(handler), default(TInterface));
		}

		public static TInterface CreateProxyForTarget<TInterface>(IInterceptor interceptor, TInterface target)
		{
			var typeBuilder = types.GetOrAdd(typeof (TInterface), _ => EmitProxyForTarget<TInterface>());
			return (TInterface) Activator.CreateInstance(typeBuilder, interceptor, target);
		}

		private static Type EmitProxyForTarget<TInterface>()
		{
			if (!typeof (TInterface).IsInterface)
				throw new InvalidOperationException(string.Format("{0} is not an interface type", typeof (TInterface)));
			var typeBuilder = module.DefineType(typeof (TInterface) + "_proxy", TypeAttributes.Public);
			var originalMethods = typeof (TInterface).GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.OrderBy(x => x.Name)
				.ThenBy(x => string.Join("_", x.GetParameters().Select(p => p.ParameterType.Name).ToArray()))
				.ToArray();
			typeBuilder.AddInterfaceImplementation(typeof (TInterface));
			var interceptorField = typeBuilder.DefineField("interceptor", typeof (IInterceptor), FieldAttributes.Private);
			var proxyField = typeBuilder.DefineField("proxy", typeof (TInterface), FieldAttributes.Private);
			var methodInfosField = typeBuilder.DefineField("methods", typeof (MethodBase).MakeArrayType(),
				FieldAttributes.Private);
			var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis,
				new[] {typeof (IInterceptor), typeof (TInterface)});
			var ctorIl = ctor.GetILGenerator();
			ctorIl.Emit(OpCodes.Ldarg_0);
			ctorIl.Emit(OpCodes.Ldarg_1);
			ctorIl.Emit(OpCodes.Stfld, interceptorField);
			ctorIl.Emit(OpCodes.Ldarg_0);
			ctorIl.Emit(OpCodes.Ldarg_2);
			ctorIl.Emit(OpCodes.Stfld, proxyField);
			ctorIl.Emit(OpCodes.Ldarg_0);
			EmitIntConst(ctorIl, originalMethods.Length);
			ctorIl.Emit(OpCodes.Newarr, typeof (MethodBase));
			ctorIl.Emit(OpCodes.Stfld, methodInfosField);
			for (var index = 0; index < originalMethods.Length; index++)
			{
				var originalMethod = originalMethods[index];
				ctorIl.Emit(OpCodes.Ldarg_0);
				ctorIl.Emit(OpCodes.Ldfld, methodInfosField);
				EmitIntConst(ctorIl, index);
				EmitLoadMethodInfo(ctorIl, originalMethod, typeof (TInterface));
				ctorIl.Emit(OpCodes.Stelem, typeof (MethodBase));
			}
			ctorIl.Emit(OpCodes.Ret);
			for (var index = 0; index < originalMethods.Length; index++)
			{
				var originalMethod = originalMethods[index];
				var proxyImplMethod = typeBuilder.DefineMethod(originalMethod.Name,
					originalMethod.Attributes & ~MethodAttributes.Abstract,
					originalMethod.CallingConvention, originalMethod.ReturnType,
					originalMethod.GetParameters().Select(c => c.ParameterType).ToArray());
				if (originalMethod.IsGenericMethod)
				{
					var originalGenericArgs = originalMethod.GetGenericArguments();
					var genericParams = proxyImplMethod.DefineGenericParameters(originalGenericArgs.Select(x => "T" + x.Name).ToArray());
					foreach (
						var tuple in
							genericParams.Zip(
								originalGenericArgs, Tuple.Create))
					{
						var interfaceConstraints = tuple.Item2.GetGenericParameterConstraints().Where(x => x.IsInterface).ToArray();
						var baseTypeConstraint = tuple.Item2.GetGenericParameterConstraints().FirstOrDefault(x => x.IsClass);
						if (baseTypeConstraint != null)
							tuple.Item1.SetBaseTypeConstraint(baseTypeConstraint);
						tuple.Item1.SetInterfaceConstraints(interfaceConstraints);
					}
				}
				var ilg = proxyImplMethod.GetILGenerator();
				// load interceptor
				ilg.Emit(OpCodes.Ldarg_0);
				ilg.Emit(OpCodes.Ldfld, interceptorField);
				//new interceptor args
				ilg.Emit(OpCodes.Ldarg_0);
				ilg.Emit(OpCodes.Ldfld, proxyField);
				
				//ilg.Emit(OpCodes.Ldnull);
				EmitNewMethodInvocation(ilg, originalMethod, methodInfosField, index);
				
				var interceptorArgsCtor =
					GetInterceptorType(originalMethod).GetConstructor(new[] {typeof (object), typeof (MethodInvocation)});
				ilg.Emit(OpCodes.Newobj, interceptorArgsCtor);
				ilg.DeclareLocal(typeof (InterceptorArgs));
				ilg.Emit(OpCodes.Stloc_0);
				ilg.Emit(OpCodes.Ldloc_0);
				// call interceptor handle method
				ilg.Emit(OpCodes.Callvirt, typeof (IInterceptor).GetMethod("Handle"));
				//get return value
				if (originalMethod.ReturnType != typeof (void))
				{
					ilg.Emit(OpCodes.Ldloc_0);
					ilg.Emit(OpCodes.Ldfld, typeof (InterceptorArgs).GetField("Result"));
					ilg.Emit(originalMethod.ReturnType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, originalMethod.ReturnType);
				}
				ilg.Emit(OpCodes.Ret);
			}
			return typeBuilder.CreateType();
		}

		private static void EmitNewMethodInvocation(ILGenerator generator, MethodInfo methodInfo, FieldInfo methodInfosField, int methodIndex)
		{
			//load name
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldfld, methodInfosField);
			EmitIntConst(generator, methodIndex);
			generator.Emit(OpCodes.Ldelem, typeof (MethodBase));
			//load parameters
			
			var parameters = methodInfo.GetParameters();
			if (parameters.Length > 0)
			{
				EmitIntConst(generator, methodInfo.GetParameters().Length);
				generator.Emit(OpCodes.Newarr, typeof(object));
				for (var index = 0; index < parameters.Length; index++)
				{
					var parameter = parameters[index];
					generator.Emit(OpCodes.Dup);
					EmitIntConst(generator, index);
					generator.Emit(OpCodes.Ldarg, index + 1);
					if (parameter.ParameterType.IsValueType)
						generator.Emit(OpCodes.Box, parameter.ParameterType);
					generator.Emit(OpCodes.Stelem, typeof(object));
				}
				generator.Emit(OpCodes.Newobj, typeof(MethodInvocation).GetConstructor(new[] { typeof(MethodInfo), typeof(object[]) }));
			}
			else 
				generator.Emit(OpCodes.Newobj, typeof(MethodInvocation).GetConstructor(new[] { typeof(MethodInfo)}));
		}

		private static void EmitLoadMethodInfo(ILGenerator generator, MethodInfo methodInfo, Type interfaceType)
		{
			generator.Emit(OpCodes.Ldtoken, methodInfo);
			generator.Emit(OpCodes.Ldtoken, interfaceType);
			generator.Emit(OpCodes.Call,
				typeof (MethodBase).GetMethod("GetMethodFromHandle", BindingFlags.Static | BindingFlags.Public, null,
					new[] {typeof (RuntimeMethodHandle), typeof (RuntimeTypeHandle)}, new ParameterModifier[0]));
		}

		private static Type GetInterceptorType(MethodInfo methodInfo)
		{
			return interceptorArgsTypes.GetOrAdd(methodInfo, EmitInterceptorType);
		}

		private static Type EmitInterceptorType(MethodInfo proxiedMethod)
		{
			var name = "InterceptorArgs_"
			           + proxiedMethod.Name
			           + string.Join("_", proxiedMethod.GetParameters().Select(x => x.ParameterType.Name))
			           + "_" + Interlocked.Increment(ref counter);
			var typeBuilder = module.DefineType(name, TypeAttributes.Public, typeof (InterceptorArgs));

			var baseConstructor =
				typeof (InterceptorArgs).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
			var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis,
				baseConstructor.GetParameters().Select(x => x.ParameterType).ToArray())
				.GetILGenerator();
			constructor.Emit(OpCodes.Ldarg_0);
			constructor.Emit(OpCodes.Ldarg_1);
			constructor.Emit(OpCodes.Ldarg_2);
			constructor.Emit(OpCodes.Call, baseConstructor);
			constructor.Emit(OpCodes.Ret);
			var proceedIl =
				typeBuilder.DefineMethod("Proceed", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
					CallingConventions.HasThis).GetILGenerator();
			proceedIl.Emit(OpCodes.Ldarg_0);
			proceedIl.Emit(OpCodes.Ldfld,
				typeof (InterceptorArgs).GetField("proxy", BindingFlags.NonPublic | BindingFlags.Instance));
			var parameters = proxiedMethod.GetParameters();
			for (var i = 0; i < parameters.Length; i++)
			{
				var parameter = parameters[i];
				proceedIl.Emit(OpCodes.Ldarg_0);
				proceedIl.Emit(OpCodes.Ldfld, typeof (InterceptorArgs).GetField("Invocation"));
				proceedIl.Emit(OpCodes.Ldfld, typeof (MethodInvocation).GetField("Arguments"));
				EmitIntConst(proceedIl, i);
				proceedIl.Emit(OpCodes.Ldelem, typeof (object));
				proceedIl.Emit(parameter.ParameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameter.ParameterType);
			}
			proceedIl.Emit(OpCodes.Callvirt, proxiedMethod);
			if (proxiedMethod.ReturnType != typeof (void))
			{
				proceedIl.DeclareLocal(proxiedMethod.ReturnType);
				proceedIl.Emit(OpCodes.Stloc_0);
				proceedIl.Emit(OpCodes.Ldarg_0);
				proceedIl.Emit(OpCodes.Ldloc_0);
				if (proxiedMethod.ReturnType.IsValueType)
					proceedIl.Emit(OpCodes.Box, proxiedMethod.ReturnType);
				proceedIl.Emit(OpCodes.Stfld, typeof (InterceptorArgs).GetField("Result"));
			}
			proceedIl.Emit(OpCodes.Ret);
			return typeBuilder.CreateType();
		}

		private static void EmitIntConst(ILGenerator ilGenerator, int i)
		{
			switch (i)
			{
				case 0:
					ilGenerator.Emit(OpCodes.Ldc_I4_0);
					break;
				case 1:
					ilGenerator.Emit(OpCodes.Ldc_I4_1);
					break;
				case 2:
					ilGenerator.Emit(OpCodes.Ldc_I4_2);
					break;
				case 3:
					ilGenerator.Emit(OpCodes.Ldc_I4_3);
					break;
				case 4:
					ilGenerator.Emit(OpCodes.Ldc_I4_4);
					break;
				case 5:
					ilGenerator.Emit(OpCodes.Ldc_I4_5);
					break;
				case 6:
					ilGenerator.Emit(OpCodes.Ldc_I4_6);
					break;
				case 7:
					ilGenerator.Emit(OpCodes.Ldc_I4_7);
					break;
				case 8:
					ilGenerator.Emit(OpCodes.Ldc_I4_8);
					break;
				default:
					ilGenerator.Emit(OpCodes.Ldc_I4, i);
					break;
			}
		}

		public interface IHandler
		{
			object Handle(MethodInvocation invocation);
		}

		public interface IInterceptor
		{
			void Handle(InterceptorArgs args);
		}

		public abstract class InterceptorArgs
		{
			public MethodInvocation Invocation;
			protected object proxy;
			public object Result;

			protected InterceptorArgs(object proxy, MethodInvocation invocation)
			{
				this.proxy = proxy;
				Invocation = invocation;
			}

			public abstract void Proceed();
		}

		private class Adapter : IInterceptor
		{
			private readonly IHandler handler;

			public Adapter(IHandler handler)
			{
				this.handler = handler;
			}

			public void Handle(InterceptorArgs args)
			{
				args.Result = handler.Handle(args.Invocation);
			}
		}

		public struct MethodInvocation
		{
			private static readonly object[] EmptyArgs = new object[0];

			public MethodInvocation(MethodInfo methodInfo)
			{
				MethodInfo = methodInfo;
				Arguments = EmptyArgs;
			}

			public MethodInvocation(MethodInfo methodInfo, object[] arguments)
			{
				Arguments = arguments;
				MethodInfo = methodInfo;
			}

			public readonly object[] Arguments;
			public readonly MethodInfo MethodInfo;
		}
	}
}