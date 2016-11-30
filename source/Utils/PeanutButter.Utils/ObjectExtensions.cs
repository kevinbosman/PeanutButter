﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
// ReSharper disable MemberCanBePrivate.Global

namespace PeanutButter.Utils
{
    public static class ObjectExtensions
    {
        [Obsolete("AllPropertiesMatch has been deprecated in favour of the more powerful DeepEquals; use DeepSubEquals if your source properties are potentially a subset of the comparison properties")]
        public static bool AllPropertiesMatch(this object objSource, object objCompare, params string[] ignorePropertiesByName)
        {
            return objSource.DeepEquals(objCompare, ignorePropertiesByName);
        }

        /// <summary>
        /// Runs a deep equality test between two objects, glossing over reference
        /// differences between class-types and comparing only primitive types. Use
        /// this when you'd like to essentially test whether the data in one object
        /// hierachy matches that of another
        /// </summary>
        /// <param name="objSource">Object which is the source of truth</param>
        /// <param name="objCompare">Object to compare with</param>
        /// <param name="ignorePropertiesByName">Params array of properties to ignore by name</param>
        /// <returns></returns>
        public static bool DeepEquals(this object objSource, object objCompare, params string[] ignorePropertiesByName)
        {
            return new DeepEqualityTester(
                objSource,
                objCompare,
                ignorePropertiesByName
            ).AreDeepEqual();
        }

        public static bool DeepSubEquals(this object objSource, object objCompare, params string[] ignorePropertiesByName)
        {
            var tester = new DeepEqualityTester(
                objSource,
                objCompare,
                ignorePropertiesByName
            );
            tester.FailOnMissingProperties = false;
            return tester.AreDeepEqual();
        }

        public static bool DeepIntersectionEquals(this object objSource, object objCompare, params string[] ignorePropertiesByName)
        {
            var tester = new DeepEqualityTester(
                objSource,
                objCompare,
                ignorePropertiesByName
            );
            tester.OnlyTestIntersectingProperties = true;
            return tester.AreDeepEqual();
        }


        [Obsolete("Please use ContainsOneDeepEqualTo")]
        public static bool ContainsOneLike<T1, T2>(this IEnumerable<T1> collection, T2 item)
        {
            return collection.ContainsOneDeepEqualTo(item);
        }

        public static bool ContainsOneDeepEqualTo<T1, T2>(
            this IEnumerable<T1> collection, 
            T2 item,
            params string[] ignoreProperties)
        {
            return collection.Any(i => i.DeepEquals(item, ignoreProperties));
        }

        public static bool ContainsOneIntersectionEqualTo<T1, T2>(
            this IEnumerable<T1> collection,
            T2 item,
            params string[] ignoreProperties
        )
        {
            return collection.Any(i => i.DeepIntersectionEquals(item, ignoreProperties));
        }

        public static bool ContainsOnlyOneDeepEqualTo<T1, T2>(
            this IEnumerable<T1> collection, 
            T2 item,
            params string[] ignoreProperties)
        {
            return collection.ContainsOnlyOneMatching(item, 
                (t1, t2) => t1.DeepEquals(t2, ignoreProperties));
        }

        public static bool ContainsOnlyOneIntersectionEqualTo<T1, T2>(
            this IEnumerable<T1> collection,
            T2 item,
            params string[] ignoreProperties
        )
        {
            return collection.ContainsOnlyOneMatching(item, 
                (t1, t2) => t1.DeepIntersectionEquals(t2, ignoreProperties));
        }

        public static bool ContainsOnlyOneMatching<T1, T2>(
            this IEnumerable<T1> collection,
            T2 item,
            Func<T1, T2, bool> comparer
        )
        {
            return collection.Aggregate(0, (acc, cur) =>
            {
                if (acc > 1) return acc;
                acc += comparer(cur, item) ? 1 : 0;
                return acc;
            }) == 1;
        }

        public static void CopyPropertiesTo(this object src, object dst)
        {
            src.CopyPropertiesTo(dst, true);
        }

        public static void CopyPropertiesTo(this object src, object dst, params string[] ignoreProperties)
        {
            src.CopyPropertiesTo(dst, true, ignoreProperties);
        }

        public static void CopyPropertiesTo(this object src, object dst, bool deep, params string[] ignoreProperties)
        {
            if (src == null || dst == null) return;
            var srcPropInfos = src.GetType().GetProperties()
                                .Where(pi => !ignoreProperties.Contains(pi.Name));
            var dstPropInfos = dst.GetType().GetProperties();

            foreach (var srcPropInfo in srcPropInfos.Where(pi => pi.CanRead))
            {
                var matchingTarget = dstPropInfos.FirstOrDefault(dp => dp.Name == srcPropInfo.Name &&
                                                                       dp.PropertyType == srcPropInfo.PropertyType &&
                                                                       dp.CanWrite);
                if (matchingTarget == null) continue;

                var srcVal = srcPropInfo.GetValue(src, null);
                if (!deep || IsSimpleTypeOrNullableOfSimpleType(srcPropInfo.PropertyType))
                {
                    matchingTarget.SetValue(dst, srcVal, null);
                }
                else if (srcPropInfo.PropertyType.IsArrayOrAssignableFromArray())
                {
                    var underlyingType = srcPropInfo.PropertyType.GetCollectionItemType();
                    if (underlyingType != null)
                    {
                        var specific = _genericMakeArrayCopy.MakeGenericMethod(underlyingType);
                        var newValue = specific.Invoke(null, new object[] { srcVal });
                        matchingTarget.SetValue(dst, newValue);
                    }
                }
                else
                {
                    if (srcVal != null)
                    {
                        var targetVal = matchingTarget.GetValue(dst, null);
                        srcVal.CopyPropertiesTo(targetVal);
                    }
                    else
                    {
                        matchingTarget.SetValue(dst, null, null);
                    }
                }
            }
        }

        private static readonly MethodInfo _genericMakeArrayCopy
            = typeof(ObjectExtensions).GetMethod("MakeArrayCopyOf", BindingFlags.NonPublic | BindingFlags.Static);
        private static T[] MakeArrayCopyOf<T>(IEnumerable<T> src)
        {
            try
            {
                var result = new T[src?.Count() ?? 0];
                if (src != null)
                {
                    var idx = 0;
                    foreach (var item in src)
                    {
                        result[idx++] = item;
                    }
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSimpleTypeOrNullableOfSimpleType(Type t)
        {
            return Types.Primitives.Any(si => si == t ||
                                          (t.IsGenericType &&
                                          t.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                                          Nullable.GetUnderlyingType(t) == si));
        }

        public static T Get<T>(this object src, string propertyPath)
        {
            var type = src.GetType();
            return ResolvePropertyValueFor<T>(src, propertyPath, type);
        }

        public static T GetOrDefault<T>(this object src, string propertyPath, T defaultValue = default(T))
        {
            try
            {
                return Get<T>(src, propertyPath);
            }
            catch (PropertyNotFoundException)
            {
                return defaultValue;
            }
        }

        public static T[] AsArray<T>(this T input)
        {
            return new[] { input };
        }


        private static T ResolvePropertyValueFor<T>(object src, string propertyPath, Type type)
        {
            var parts = propertyPath.Split('.');
            var valueAsObject = parts.Aggregate(src, GetPropertyValue);
            var valueType = valueAsObject.GetType();
            if (!valueType.IsAssignableTo<T>())
                throw new ArgumentException(
                    "Get<> must be invoked with a type to which the property value could be assigned ("
                    + type.Name + "." + propertyPath + " has type '" + valueType.Name
                    + "', but expected '" + typeof(T).Name + "' or derivative");
            return (T)valueAsObject;
        }


        public static object GetPropertyValue(this object src, string propertyName)
        {
            var type = src.GetType();
            var propInfo = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(pi => pi.Name == propertyName);
            if (propInfo == null)
                throw new PropertyNotFoundException(type, propertyName);
            return propInfo.GetValue(src, null);
        }

        public static void SetPropertyValue(this object propValue, string propertyPath, object newValue)
        {
            var queue = new Queue<string>(propertyPath.Split('.'));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var type = propValue.GetType();
                var propInfo = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                    .FirstOrDefault(pi => pi.Name == current);
                if (propInfo == null)
                    throw new PropertyNotFoundException(type, current);
                if (queue.Count == 0)
                {
                    propInfo.SetValue(propValue, newValue);
                    return;
                }
                propValue = propInfo.GetValue(propValue);
            }
            throw new PropertyNotFoundException(propValue.GetType(), propertyPath);
        }

        public static T GetPropertyValue<T>(this object src, string propertyName)
        {
            var objectResult = GetPropertyValue(src, propertyName);
            return (T)objectResult;
        }

        public static bool IsAssignableTo<T>(this Type type)
        {
            return type.IsAssignableFrom(typeof(T));
        }

        public static decimal TruncateTo(this decimal value, int places)
        {
            var mul = new decimal(Math.Pow(10, places));
            return Math.Truncate(value * mul) / mul;
        }
    }
}
