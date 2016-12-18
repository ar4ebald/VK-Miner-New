using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace VK_Miner.VK.Model
{
    public abstract class VkObject
    {
        public delegate void InitializerDelegate<in T>(T obj, JToken value) where T : VkObject;

        static VkObject()
        {
            var info = typeof(VkObject).GetTypeInfo();
            VkObjectArrayCasterMethodInfo = info.GetDeclaredMethod(nameof(VkObjectJArrayCaster));
            SimpleJArrayCasterMethodInfo = info.GetDeclaredMethod(nameof(SimpleJArrayCaster));
        }

        private static T[] SimpleJArrayCaster<T>(JArray array)
        {
            var result = new T[array.Count];
            for (var i = 0; i < array.Count; i++)
                result[i] = array[i].Value<T>();
            return result;
        }
        private static T[] VkObjectJArrayCaster<T>(JArray array, InitializerDelegate<T> initializer) where T : VkObject, new()
        {
            if (array == null)
                return new T[0];

            var result = new T[array.Count];
            for (var i = 0; i < array.Count; i++)
            {
                result[i] = new T();
                initializer(result[i], array[i]);
            }
            return result;
        }

        private static readonly MethodInfo SimpleJArrayCasterMethodInfo;
        private static readonly MethodInfo VkObjectArrayCasterMethodInfo;

        public static InitializerDelegate<T> CreateInitializer<T>() where T : VkObject
        {
            var type = typeof(T);
            var objParam = Expression.Parameter(type);
            var localJson = Expression.Parameter(typeof(JToken));
            var localToken = Expression.Variable(typeof(JToken));

            var vkObjectInfo = typeof(VkObject).GetTypeInfo();

            var blockBody = new List<Expression>();

            foreach (var property in type.GetRuntimeProperties()
                .Where(property => property.CanWrite && property.SetMethod.IsPublic &&
                    property.GetCustomAttribute<NonModelAttribute>() == null))
            {
                var propertyInfo = property.PropertyType.GetTypeInfo();

                Expression left = Expression.MakeMemberAccess(objParam, property);
                Expression right;

                if (propertyInfo.IsArray)
                {
                    var elementType = propertyInfo.GetElementType();
                    var elementTypeInfo = elementType.GetTypeInfo();
                    if (vkObjectInfo.IsAssignableFrom(elementTypeInfo))
                    {
                        right = Expression.Call(null,
                            VkObjectArrayCasterMethodInfo.MakeGenericMethod(elementType),
                            Expression.Call(
                                localJson,
                                "Value",
                                new[] { typeof(JArray) },
                                Expression.Constant(property.Name.CamelCaseToSnakeCase(), typeof(string))),
                            Expression.Constant(
                                elementTypeInfo.GetDeclaredField("Initializer").GetValue(null)));
                    }
                    else
                    {
                        right = Expression.Call(null,
                            SimpleJArrayCasterMethodInfo.MakeGenericMethod(elementType),
                            Expression.Call(localJson,
                                "Value",
                                new[] { typeof(JArray) },
                                Expression.Constant(property.Name.CamelCaseToSnakeCase(), typeof(string))));
                    }
                }
                else if (vkObjectInfo.IsAssignableFrom(propertyInfo))
                {
                    var constructor = property.PropertyType.GetTypeInfo().DeclaredConstructors.First(c =>
                    {
                        var args = c.GetParameters();
                        return args.Length == 1 && args[0].ParameterType == typeof(JToken);
                    });

                    blockBody.Add(
                        Expression.Assign(localToken,
                            Expression.Call(localJson,
                                "Value",
                                new[] { typeof(JToken) },
                                Expression.Constant(property.Name.CamelCaseToSnakeCase(), typeof(string)))));

                    right = Expression.Condition(
                        Expression.Equal(localToken, Expression.Constant(null, typeof(JToken))),
                        Expression.Constant(null, property.PropertyType),
                        Expression.New(
                            constructor,
                            localToken)
                        );
                }
                else if (propertyInfo.IsEnum)
                {
                    right = Expression.Convert(
                        Expression.Call(localJson,
                            "Value",
                            new[] { typeof(int) },
                            Expression.Constant(property.Name.CamelCaseToSnakeCase(), typeof(string))),
                        property.PropertyType);
                }
                else if (property.GetCustomAttribute<VkConverterAttribute>() != null)
                {
                    const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                    var methodName = property.GetCustomAttribute<VkConverterAttribute>().ConverterName;
                    var converter = type.GetMethod(methodName, flags);

#if DEBUG
                    var args = converter.GetParameters();
                    if (converter.ReturnType != property.PropertyType || args.Length != 1 || args[0].ParameterType != typeof(JToken))
                    {
                        Debugger.Break();
                        throw new Exception();
                    }
#endif
                    var json = Expression.Call(localJson, "Value", new[] { typeof(JToken) },
                        Expression.Constant(property.Name.CamelCaseToSnakeCase(), typeof(string)));
                    right = Expression.Call(converter, json);
                }
                else
                {
                    right = Expression.Call(localJson,
                        "Value",
                        new[] { property.PropertyType },
                        Expression.Constant(property.Name.CamelCaseToSnakeCase(), typeof(string)));
                }

                blockBody.Add(Expression.Assign(left, right));
            };

            return Expression.Lambda<InitializerDelegate<T>>(
                Expression.Block(new[] { localToken }, blockBody), objParam, localJson)
                .Compile();
        }
    }
}
