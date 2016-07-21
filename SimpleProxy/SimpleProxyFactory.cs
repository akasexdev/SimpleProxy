using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace SimpleProxy
{
	public static class SimpleProxyFactory
	{
		private static readonly ModuleBuilder module;
		private static int interceptorTypeNamesUniquifier;
		private static int proxyTypeNamesUniquifier;
		private static readonly ConcurrentDictionary<MethodInfo, Type> interceptorArgsTypes =
			new ConcurrentDictionary<MethodInfo, Type>();

		private static readonly ConcurrentDictionary<CacheKey, Type> types =
			new ConcurrentDictionary<CacheKey, Type>();

		static SimpleProxyFactory()
		{
			var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(typeof (SimpleProxyFactory).Name),
				AssemblyBuilderAccess.RunAndCollect);
			module = assemblyBuilder.DefineDynamicModule("main");
		}

		public static TInterface CreateProxyWithoutTarget<TInterface>(IHandler handler, params Type[] additionalInterfaces)
		{
			var interfaces = new[] { typeof(TInterface) }.Concat(additionalInterfaces).ToArray();
			var typeBuilder = types.GetOrAdd(new CacheKey(interfaces, false), key => EmitProxy(key.Interfaces, new WithoutTargetEmitter()));
			return (TInterface) Activator.CreateInstance(typeBuilder, handler);
		}

		public static TInterface CreateProxyForTarget<TInterface>(IInterceptor interceptor, object target, params Type[] additionalInterfaces)
		{
			var interfaces = new[] { typeof (TInterface) }.Concat(additionalInterfaces).ToArray();
			foreach (var @interface in interfaces)
			{
				if (!@interface.IsInstanceOfType(target))
					throw new InvalidOperationException(string.Format("Target object does not implement interface {0}", @interface.Name));
			}
			var typeBuilder = types.GetOrAdd(new CacheKey(interfaces, true), key => EmitProxy(key.Interfaces, new ForTargetEmitter()));
			return (TInterface) Activator.CreateInstance(typeBuilder, interceptor, target);
		}

		private static Type EmitProxy(Type[] interfaces, IEmitter emitter)
		{
			foreach (var @interface in interfaces)
			{
				if (!@interface.IsInterface)
					throw new InvalidOperationException(string.Format("{0} is not an interface type", @interface.Name));
				if (!@interface.IsVisible)
					throw new InvalidOperationException(string.Format("Interface {0} is not public", @interface.Name));
			}
			
			var name = new[]
					   {
						   "proxy_for",
						   "(" + interfaces.Select(c => c.Name).JoinStrings(",") + ")" +
						   emitter.GetType().Name,
						   Interlocked.Increment(ref proxyTypeNamesUniquifier).ToString()
					   }
				.JoinStrings("_");
			var typeBuilder = module.DefineType(name, TypeAttributes.Public);
			foreach (var @interface in interfaces)
				typeBuilder.AddInterfaceImplementation(@interface);

			var methodInfosField = typeBuilder.DefineField("methods", typeof (MethodBase).MakeArrayType(),
				FieldAttributes.Private | FieldAttributes.Static);
			var methodsMap = interfaces.SelectMany(i => i.GetMethods(BindingFlags.Instance | BindingFlags.Public))
				.Select((m, i) => new MethodContext(m, i, methodInfosField))
				.ToArray();
			//initialize static methods field in static ctor to avoid runtime GetMethodFromHandle call
			InitMethodsField(typeBuilder, methodInfosField, methodsMap);
			emitter.HandleType(typeBuilder);
			
			foreach (var methodContext in methodsMap)
			{
				var originalMethod = methodContext.OriginalMethod;
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
				emitter.HandleMethod(proxyImplMethod.GetILGenerator(), methodContext);
			}
			return typeBuilder.CreateType();
		}

		private static void InitMethodsField(TypeBuilder typeBuilder, FieldBuilder methodInfosField, MethodContext[] methodsMap)
		{
			var ctor = typeBuilder.DefineConstructor(MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
			var ctorIl = ctor.GetILGenerator();
			ctorIl.Emit(OpCodes.Ldnull);
			EmitIntConst(ctorIl, methodsMap.Length);
			ctorIl.Emit(OpCodes.Newarr, typeof(MethodBase));
			foreach (var methodContext in methodsMap)
			{
				ctorIl.Emit(OpCodes.Dup);
				EmitIntConst(ctorIl, methodContext.Index);
				ctorIl.Emit(OpCodes.Ldtoken, methodContext.OriginalMethod);
				ctorIl.Emit(OpCodes.Ldtoken, methodContext.OriginalMethod.DeclaringType);
				ctorIl.Emit(OpCodes.Call, ExpressionHelpers.GetMethod(() => MethodBase.GetMethodFromHandle(default(RuntimeMethodHandle), default(RuntimeTypeHandle))));
				ctorIl.Emit(OpCodes.Stelem_Ref);
			}
			ctorIl.Emit(OpCodes.Stfld, methodInfosField);
			ctorIl.Emit(OpCodes.Ret);
		}

		private static void EmitNewMethodInvocation(ILGenerator generator, MethodContext methodContext)
		{
			//load methodInfo
			methodContext.EmitLoadMethodInfo(generator);

			//load parameters
			var parameters = methodContext.OriginalMethod.GetParameters();
			if (parameters.Length > 0)
			{
				EmitIntConst(generator, parameters.Length);
				generator.Emit(OpCodes.Newarr, typeof (object));
				for (var index = 0; index < parameters.Length; index++)
				{
					var parameter = parameters[index];
					generator.Emit(OpCodes.Dup);
					EmitIntConst(generator, index);
					generator.Emit(OpCodes.Ldarg, index + 1);
					if (parameter.ParameterType.IsValueType)
						generator.Emit(OpCodes.Box, parameter.ParameterType);
					generator.Emit(OpCodes.Stelem_Ref);
				}
			}
			else
				generator.Emit(OpCodes.Ldnull);
			generator.Emit(OpCodes.Newobj, ExpressionHelpers.GetConstructor(() => new MethodInvocation(null, null)));
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

		private interface IEmitter
		{
			void HandleType(TypeBuilder typeBuilder);
			void HandleMethod(ILGenerator methodIl, MethodContext methodContext);
		}

		private class ForTargetEmitter: IEmitter
		{
			private FieldBuilder proxyField;
			private FieldBuilder interceptorField;

			public void HandleType(TypeBuilder typeBuilder)
			{
				interceptorField = typeBuilder.DefineField("interceptor", typeof (IInterceptor), FieldAttributes.Private);
				proxyField = typeBuilder.DefineField("proxy", typeof (object), FieldAttributes.Private);

				var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis,
					new[] { typeof (IInterceptor), typeof (object) });
				var ctorIl = ctor.GetILGenerator();
				ctorIl.Emit(OpCodes.Ldarg_0);
				ctorIl.Emit(OpCodes.Ldarg_1);
				ctorIl.Emit(OpCodes.Stfld, interceptorField);
				ctorIl.Emit(OpCodes.Ldarg_0);
				ctorIl.Emit(OpCodes.Ldarg_2);
				ctorIl.Emit(OpCodes.Stfld, proxyField);
				ctorIl.Emit(OpCodes.Ret);
			}

			public void HandleMethod(ILGenerator methodIl, MethodContext methodContext)
			{
				// load interceptor
				methodIl.Emit(OpCodes.Ldarg_0);
				methodIl.Emit(OpCodes.Ldfld, interceptorField);

				//load proxy
				methodIl.Emit(OpCodes.Ldarg_0);
				methodIl.Emit(OpCodes.Ldfld, proxyField);

				//new interceptor args
				EmitNewMethodInvocation(methodIl, methodContext);

				var interceptorArgsCtor =
					GetInterceptorArgsType(methodContext.OriginalMethod).GetConstructor(new[] { typeof(object), typeof(MethodInvocation) });
				methodIl.Emit(OpCodes.Newobj, interceptorArgsCtor);
				methodIl.DeclareLocal(typeof(InterceptorArgs));
				methodIl.Emit(OpCodes.Stloc_0);
				methodIl.Emit(OpCodes.Ldloc_0);
				// call interceptor handle method
				methodIl.Emit(OpCodes.Callvirt, ExpressionHelpers.GetMethod<IInterceptor>(i => i.Handle(null)));
				//get return value
				if (methodContext.OriginalMethod.ReturnType != typeof(void))
				{
					methodIl.Emit(OpCodes.Ldloc_0);
					methodIl.Emit(OpCodes.Ldfld, ExpressionHelpers.GetField<InterceptorArgs, object>(c => c.Result));
					methodIl.Emit(methodContext.OriginalMethod.ReturnType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, methodContext.OriginalMethod.ReturnType);
				}
				
				methodIl.Emit(OpCodes.Ret);
			}

			private static Type GetInterceptorArgsType(MethodInfo methodInfo)
			{
				return interceptorArgsTypes.GetOrAdd(methodInfo, EmitInterceptorArgsType);
			}

			private static Type EmitInterceptorArgsType(MethodInfo proxiedMethod)
			{
				var name = new[]
						   {
							   "InterceptorArgs", proxiedMethod.DeclaringType.Name,
							   proxiedMethod.Name, Interlocked.Increment(ref interceptorTypeNamesUniquifier).ToString()
						   }
					.JoinStrings("_");
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
				EmitProceedMethodImpl(proxiedMethod, typeBuilder);
				return typeBuilder.CreateType();
			}

			private static void EmitProceedMethodImpl(MethodInfo originalMethod, TypeBuilder typeBuilder)
			{
				var proceedIl =
					typeBuilder.DefineMethod("Proceed", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
						CallingConventions.HasThis).GetILGenerator();
				proceedIl.Emit(OpCodes.Ldarg_0);
				proceedIl.Emit(OpCodes.Ldfld, ExpressionHelpers.GetField<InterceptorArgs, object>(c => c.proxy));
				//load args
				var parameters = originalMethod.GetParameters();
				for (var i = 0; i < parameters.Length; i++)
				{
					var parameter = parameters[i];
					proceedIl.Emit(OpCodes.Ldarg_0);
					proceedIl.Emit(OpCodes.Ldfld, ExpressionHelpers.GetField<InterceptorArgs, MethodInvocation>(c => c.Invocation));
					proceedIl.Emit(OpCodes.Ldfld, ExpressionHelpers.GetField<MethodInvocation, object[]>(c => c.Arguments));
					EmitIntConst(proceedIl, i);
					proceedIl.Emit(OpCodes.Ldelem_Ref);
					proceedIl.Emit(parameter.ParameterType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, parameter.ParameterType);
				}
				proceedIl.Emit(OpCodes.Callvirt, originalMethod);
				//set return value
				if (originalMethod.ReturnType != typeof(void))
				{
					proceedIl.DeclareLocal(originalMethod.ReturnType);
					proceedIl.Emit(OpCodes.Stloc_0);
					proceedIl.Emit(OpCodes.Ldarg_0);
					proceedIl.Emit(OpCodes.Ldloc_0);
					if (originalMethod.ReturnType.IsValueType)
						proceedIl.Emit(OpCodes.Box, originalMethod.ReturnType);
					proceedIl.Emit(OpCodes.Stfld, ExpressionHelpers.GetField<InterceptorArgs, object>(c => c.Result));
				}
				proceedIl.Emit(OpCodes.Ret);
			}
		}

		private class WithoutTargetEmitter: IEmitter
		{
			private FieldBuilder handlerField;

			public void HandleType(TypeBuilder typeBuilder)
			{
				handlerField = typeBuilder.DefineField("handler", typeof (IHandler), FieldAttributes.Private);
				var ctor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, new[] { typeof (IHandler) });
				var ctorIl = ctor.GetILGenerator();
				ctorIl.Emit(OpCodes.Ldarg_0);
				ctorIl.Emit(OpCodes.Ldarg_1);
				ctorIl.Emit(OpCodes.Stfld, handlerField);
				ctorIl.Emit(OpCodes.Ret);
			}

			public void HandleMethod(ILGenerator methodIl, MethodContext methodContext)
			{
				methodIl.Emit(OpCodes.Ldarg_0);
				methodIl.Emit(OpCodes.Ldfld, handlerField);
				EmitNewMethodInvocation(methodIl, methodContext);
				methodIl.Emit(OpCodes.Callvirt, ExpressionHelpers.GetMethod<IHandler>(c => c.Handle(default(MethodInvocation))));
				var returnType = methodContext.OriginalMethod.ReturnType;
				if (returnType != typeof (void))
					methodIl.Emit(returnType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, returnType);
				else methodIl.Emit(OpCodes.Pop);
				methodIl.Emit(OpCodes.Ret);
			}
		}

		private struct MethodContext
		{
			private readonly FieldBuilder methodInfosField;
			public readonly int Index;
			public readonly MethodInfo OriginalMethod;

			public MethodContext(MethodInfo originalMethod, int index, FieldBuilder methodInfosField) : this()
			{
				OriginalMethod = originalMethod;
				Index = index;
				this.methodInfosField = methodInfosField;
			}

			public void EmitLoadMethodInfo(ILGenerator ilGenerator)
			{
				ilGenerator.Emit(OpCodes.Ldnull);
				ilGenerator.Emit(OpCodes.Ldfld, methodInfosField);
				EmitIntConst(ilGenerator, Index);
				ilGenerator.Emit(OpCodes.Ldelem_Ref);
			}
		}

		private struct CacheKey: IEquatable<CacheKey>
		{
			public readonly Type[] Interfaces;
			private readonly bool HasTarget;

			public CacheKey(Type[] interfaces, bool hasTarget)
			{
				Interfaces = interfaces;
				HasTarget = hasTarget;
			}

			public bool Equals(CacheKey other)
			{
				return Interfaces.IsEquivalentTo(other.Interfaces) && HasTarget == other.HasTarget;
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj)) return false;
				return obj is CacheKey && Equals((CacheKey) obj);
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return Interfaces.Aggregate(HasTarget.GetHashCode(), (hash, type) => (hash*397) ^ type.GetHashCode());
				}
			}
		}
	}
}