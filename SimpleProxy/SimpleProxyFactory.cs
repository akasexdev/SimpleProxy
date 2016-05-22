using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Kontur.Elba.Core.Utilities.Reflection
{
	public static class SimpleProxyFactory
	{
		static SimpleProxyFactory()
		{
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(typeof(SimpleProxyFactory).Name), AssemblyBuilderAccess.RunAndCollect);
			module = assemblyBuilder.DefineDynamicModule("main");
		}

		private static readonly ModuleBuilder module;

		private static readonly ConcurrentDictionary<MethodInfo, Type> interceptorArgsTypes =
			new ConcurrentDictionary<MethodInfo, Type>();

		private static readonly ConcurrentDictionary<Type, Type> types =
			new ConcurrentDictionary<Type, Type>();

		public static TInterface CreateProxyWithoutTarget<TInterface>(IHandler handler)
		{
			return CreateProxyForTarget(new Adapter(handler), default(TInterface));
		}

		public static TInterface CreateProxyForTarget<TInterface>(IInterceptor interceptor, TInterface target)
		{
			var typeBuilder = types.GetOrAdd(typeof(TInterface), _ => EmitProxyForTarget<TInterface>());
			return (TInterface)Activator.CreateInstance(typeBuilder, interceptor, target);
		}

		private static Type EmitProxyForTarget<TInterface>()
		{
			if (!typeof(TInterface).IsInterface)
				throw new InvalidOperationException(string.Format("{0} is not an interface type", typeof(TInterface)));
			var typeBuilder = module.DefineType(typeof(TInterface) + "_proxy", TypeAttributes.Public);
			var originalMethods = typeof (TInterface).GetMethods(BindingFlags.Instance | BindingFlags.Public)
				.OrderBy(x => x.Name)
				.ThenBy(x => string.Join("_", x.GetParameters().Select(p => p.ParameterType.Name).ToArray()))
				.ToArray();
			typeBuilder.AddInterfaceImplementation(typeof (TInterface));
			var interceptorField = typeBuilder.DefineField("interceptor", typeof(IInterceptor), FieldAttributes.Private);
			var proxyField = typeBuilder.DefineField("proxy", typeof (TInterface), FieldAttributes.Private);
			var methodInfosField = typeBuilder.DefineField("methods", typeof (MethodBase).MakeArrayType(),
				FieldAttributes.Private);
			var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis,
				new[] {typeof (IInterceptor), typeof (TInterface) });
			var ctorIl = ctor.GetILGenerator();
			ctorIl.Emit(OpCodes.Ldarg_0);
			ctorIl.Emit(OpCodes.Ldarg_1);
			ctorIl.Emit(OpCodes.Stfld, interceptorField);
			ctorIl.Emit(OpCodes.Ldarg_0);
			ctorIl.Emit(OpCodes.Ldarg_2);
			ctorIl.Emit(OpCodes.Stfld, proxyField);
			ctorIl.Emit(OpCodes.Ldarg_0);
			ctorIl.Emit(OpCodes.Ldc_I4, originalMethods.Length);
			ctorIl.Emit(OpCodes.Newarr, typeof(MethodBase));
			ctorIl.Emit(OpCodes.Stfld, methodInfosField);
			for (int index = 0; index < originalMethods.Length; index++)
			{
				var originalMethod = originalMethods[index];
				ctorIl.Emit(OpCodes.Ldarg_0);
				ctorIl.Emit(OpCodes.Ldfld, methodInfosField);
				ctorIl.Emit(OpCodes.Ldc_I4, index);
				EmitLoadMethodInfo(ctorIl, originalMethod, typeof (TInterface));
				ctorIl.Emit(OpCodes.Stelem, typeof (MethodBase));
			}
			ctorIl.Emit(OpCodes.Ret);
			for (int index = 0; index < originalMethods.Length; index++)
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
					foreach (var tuple in genericParams.Zip(originalGenericArgs, Tuple.Create))
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

				EmitNewMethodInvocation(ilg, originalMethod, methodInfosField, index);
				var interceptorArgsCtor =
					GetInterceptorType(originalMethod).GetConstructor(new[] {typeof (object), typeof (MethodInvocation)});
				ilg.Emit(OpCodes.Newobj, interceptorArgsCtor);
				ilg.DeclareLocal(typeof (InterceptorArgs));
				ilg.Emit(OpCodes.Stloc_0);
				ilg.Emit(OpCodes.Ldloc_0);
				// call interceptor handle method
				var interceptorHandleMethod = typeof (IInterceptor).GetMethod("Handle");
				ilg.Emit(OpCodes.Callvirt, interceptorHandleMethod);
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

		public static Func<object, object[], object> DoEmitCallOf(MethodInfo targetMethod)
		{
			var dynamicMethod = new DynamicMethod("callOf_" + targetMethod.Name,
				typeof (object),
				new[] {typeof (object), typeof (object[])},
				typeof (SimpleProxyFactory),
				true);
			var il = dynamicMethod.GetILGenerator();
			if (!targetMethod.IsStatic)
			{
				il.Emit(OpCodes.Ldarg_0);
				if (targetMethod.DeclaringType.IsValueType)
				{
					il.Emit(OpCodes.Unbox_Any, targetMethod.DeclaringType);
					il.DeclareLocal(targetMethod.DeclaringType);
					il.Emit(OpCodes.Stloc_0);
					il.Emit(OpCodes.Ldloca_S, 0);
				}
				else
					il.Emit(OpCodes.Castclass, targetMethod.DeclaringType);
			}
			var parameters = targetMethod.GetParameters();
			for (var i = 0; i < parameters.Length; i++)
			{
				il.Emit(OpCodes.Ldarg_1);
				/*if (i <= 8)
					il.Emit(ToConstant(i));
				else*/
				il.Emit(OpCodes.Ldc_I4, i);
				il.Emit(OpCodes.Ldelem_Ref);
				if (parameters[i].ParameterType.IsValueType)
					il.Emit(OpCodes.Unbox_Any, parameters[i].ParameterType);
			}
			il.Emit(dynamicMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, targetMethod);
			if (targetMethod.ReturnType == typeof (void))
				il.Emit(OpCodes.Ldnull);
			else if (targetMethod.ReturnType.IsValueType)
				il.Emit(OpCodes.Box, targetMethod.ReturnType);
			il.Emit(OpCodes.Ret);
			return (Func<object, object[], object>) dynamicMethod.CreateDelegate(typeof (Func<object, object[], object>));
		}

		private static void EmitNewMethodInvocation(ILGenerator generator, MethodInfo methodInfo, FieldBuilder methodInfosField, int methodIndex)
		{
			generator.Emit(OpCodes.Newobj, typeof(MethodInvocation).GetConstructor(Type.EmptyTypes));
			//set name
			generator.Emit(OpCodes.Dup);
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldfld, methodInfosField);
			generator.Emit(OpCodes.Ldc_I4, methodIndex);
			generator.Emit(OpCodes.Ldelem, typeof(MethodBase));
			generator.Emit(OpCodes.Stfld, typeof(MethodInvocation).GetField("MethodInfo"));
			//set parameters
			generator.Emit(OpCodes.Dup);
			generator.Emit(OpCodes.Ldc_I4, methodInfo.GetParameters().Length);
			generator.Emit(OpCodes.Newarr, typeof(object));
			var parameters = methodInfo.GetParameters();
			for (int index = 0; index < parameters.Length; index++)
			{
				var parameter = parameters[index];
				generator.Emit(OpCodes.Dup);
				generator.Emit(OpCodes.Ldc_I4, index);
				generator.Emit(OpCodes.Ldarg, index + 1);
				if (parameter.ParameterType.IsValueType)
					generator.Emit(OpCodes.Box, parameter.ParameterType);
				generator.Emit(OpCodes.Stelem, typeof(object));
			}
			generator.Emit(OpCodes.Stfld, typeof(MethodInvocation).GetField("Arguments"));
		}

		private static void EmitLoadMethodInfo(ILGenerator generator, MethodInfo methodInfo, Type interfaceType)
		{
			generator.Emit(OpCodes.Ldtoken, methodInfo);
			generator.Emit(OpCodes.Ldtoken, interfaceType);
			generator.Emit(OpCodes.Call,
						   typeof(MethodBase).GetMethod("GetMethodFromHandle", BindingFlags.Static | BindingFlags.Public, null,
						   new[] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle) }, new ParameterModifier[0]));
		}

		private static Type GetInterceptorType(MethodInfo methodInfo)
		{
			return interceptorArgsTypes.GetOrAdd(methodInfo, EmitInterceptorType);
		}

		private static Type EmitInterceptorType(MethodInfo proxiedMethod)
		{
			var typeBuilder = module.DefineType("InterceptorArgs_" + proxiedMethod.Name, TypeAttributes.Public, typeof (InterceptorArgs));
			
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
			if (proxiedMethod.ReturnType != typeof (void))
				proceedIl.Emit(OpCodes.Ldarg_0);
			proceedIl.Emit(OpCodes.Ldarg_0);
			proceedIl.Emit(OpCodes.Ldfld,
				typeof (InterceptorArgs).GetField("proxy", BindingFlags.NonPublic | BindingFlags.Instance));
			proceedIl.Emit(OpCodes.Callvirt, proxiedMethod);
			if (proxiedMethod.ReturnType != typeof (void))
				proceedIl.Emit(OpCodes.Stfld, typeof (InterceptorArgs).GetField("Result"));
			proceedIl.Emit(OpCodes.Ret);
			return typeBuilder.CreateType();
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
			protected object proxy;

			protected InterceptorArgs(object proxy, MethodInvocation invocation)
			{
				this.proxy = proxy;
				Invocation = invocation;
			}

			public MethodInvocation Invocation;//todo. field
			public object Result;

			public abstract void Proceed();
		}

		private class InterceptorArgsImpl : InterceptorArgs
		{
			public InterceptorArgsImpl(object proxy, MethodInvocation invocation) : base(proxy, invocation)
			{
			}

			public override void Proceed()
			{
				throw new NotImplementedException();
			}
		}

		private class Adapter : IInterceptor
		{
			public Adapter(IHandler handler)
			{
				this.handler = handler;
			}

			private readonly IHandler handler;

			public void Handle(InterceptorArgs args)
			{
				args.Result = handler.Handle(args.Invocation);
			}
		}

		public class MethodInvocation
		{
			public MethodInfo MethodInfo;
			public object[] Arguments;
		}
	}
}