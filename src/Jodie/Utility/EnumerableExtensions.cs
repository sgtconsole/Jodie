using System;
using System.Collections.Generic;
using System.Linq;

namespace Jodie.Utility
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> ConcatOne<T>(this IEnumerable<T> values, T value)
        {
            return values.Concat(value.ToEnumerable());
        }

        public static IEnumerable<T> ToEnumerable<T>(this T value)
        {
            // ReSharper disable once CompareNonConstrainedGenericWithNull
            return value == null ? Enumerable.Empty<T>() : new[] { value };
        }

        public static IList<T> AsList<T>(this IEnumerable<T> value)
        {
            return value as IList<T> ?? value.ToList();
        }

        public static IEnumerable<T> DefaultToEmptyIfNull<T>(this IEnumerable<T> value)
        {
            return value ?? Enumerable.Empty<T>();
        }

        public static void ForEach<T>(this IEnumerable<T> value, Action<T> action)
        {
            var list = value.DefaultToEmptyIfNull();

            foreach (var item in list)
            {
                action(item);
            }
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> value)
        {
            return new HashSet<T>(value.DefaultToEmptyIfNull());
        }

        public static bool ContainsDuplicates<T>(this IEnumerable<T> value)
        {
            var hashSet = new HashSet<T>();

            return value.Any(item => !hashSet.Add(item));
        }

        public static IEnumerable<T> FindDuplicates<T>(this IEnumerable<T> value)
        {
            var duplicates = new HashSet<T>();
            var hashSet = new HashSet<T>();

            foreach (var item in value.Where(item => !hashSet.Add(item)))
            {
                duplicates.Add(item);
            }

            return duplicates;
        }
    }
}
