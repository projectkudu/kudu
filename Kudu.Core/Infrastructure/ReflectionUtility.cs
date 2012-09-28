using System;
using System.Reflection;

namespace Kudu.Core.Infrastructure
{
    internal static class ReflectionUtility
    {
        public static PropertyInfo GetInternalProperty(Type typeName, string propertyName)
        {
            return typeName.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static MethodInfo GetInternalMethod(Type typeName, string methodName, Type[] types)
        {
            return typeName.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance, binder: null, types: types, modifiers: null);
        }

        public static T GetValue<T>(this PropertyInfo property, object instance)
        {
            return (T)property.GetValue(instance, null);
        }

        public static void SetValue(this PropertyInfo property, object instance, object value)
        {
            property.SetValue(instance, value, null);
        }
    }
}