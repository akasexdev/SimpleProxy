using System;
using System.Linq.Expressions;
using System.Reflection;

namespace SimpleProxy
{
	public static class ExpressionHelpers
	{
		public static ConstructorInfo GetConstructor<T>(Expression<Func<T>> expression)
		{
			return GetBodyOfType<NewExpression>(expression).Constructor;
		}

		public static MethodInfo GetMethod<T>(Expression<Func<T>> expression)
		{
			return GetBodyOfType<MethodCallExpression>(expression).Method;
		}

		public static MethodInfo GetMethod<T>(Expression<Action<T>> expression)
		{
			return GetBodyOfType<MethodCallExpression>(expression).Method;
		}

		public static FieldInfo GetField<TSource, TField>(Expression<Func<TSource, TField>> expression)
		{
			var xMemberAccess = GetBodyOfType<MemberExpression>(expression);
			
			var fieldInfo = xMemberAccess.Member as FieldInfo;
			if (fieldInfo == null)
				throw new InvalidOperationException(string.Format("Expected a field but was {0}", xMemberAccess.Member));
			return fieldInfo;
		}

		private static TExpression GetBodyOfType<TExpression>(LambdaExpression expression) where TExpression: class
		{
			var xLambda = expression;
			if (xLambda == null)
				throw new InvalidOperationException(string.Format("Expected a lambda expression but was {0}", expression));
			var xBody = expression.Body as TExpression;
			if (xBody == null)
				throw new InvalidOperationException(string.Format("Expected a {0} but was {1}",typeof(TExpression).Name, expression.Body));
			return xBody;
		}
	}
}