using System.Reflection;

namespace src;

public class EnumerableMethodInfoProvider : IEnumerableMethodInfoProvider
{

    public MethodInfo GetIEnumerableSelectMethodInfo()
    {
        IEnumerable<MethodInfo> selectMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "Select");

        MethodInfo selectMethodInfo =
            selectMethodInfos
                .First(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 2
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));
        return selectMethodInfo;
    }

    public MethodInfo GetIEnumerableWhereMethodInfo()
    {
        //public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
        IEnumerable<MethodInfo> whereMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "Where");

        MethodInfo whereMethodInfo =
            whereMethodInfos
                .First(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 2
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));
        return whereMethodInfo;
    }

    public MethodInfo GetIEnumerableJoinMethodInfo()
    {
        //public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector);
        IEnumerable<MethodInfo> joinMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "Join");

        MethodInfo? joinMethodInfo =
            joinMethodInfos
                .FirstOrDefault(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 5
                        && mi.GetParameters()[4].ParameterType.GetGenericTypeDefinition() == typeof(Func<,,>));
        return joinMethodInfo;
    }

    public MethodInfo GetIEnumerableAnyMethodInfo()
    {
        // public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
        IEnumerable<MethodInfo> anyMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "Any");

        MethodInfo anyMethodInfo =
            anyMethodInfos
                .First(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 2
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));
        return anyMethodInfo;
    }

    public MethodInfo GetIEnumerableCountMethodInfo()
    {
        // public static int Count<TSource>(this IEnumerable<TSource> source);
        IEnumerable<MethodInfo> anyMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "Count");

        MethodInfo anyMethodInfo =
            anyMethodInfos
                .First(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 1);
        return anyMethodInfo;
    }

    public MethodInfo GetIEnumerableGroupJoinMethodInfo()
    {
        //public static IEnumerable<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, IEnumerable<TInner>, TResult> resultSelector);
        IEnumerable<MethodInfo> anyMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "GroupJoin");

        MethodInfo anyMethodInfo =
            anyMethodInfos
                .First(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 5);
        return anyMethodInfo;
    }

    public MethodInfo GetIEnumerableSelectManyMethodInfo()
    {
        //public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector);
        IEnumerable<MethodInfo> selectManyMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "SelectMany");

        MethodInfo selectManyMethodInfo =
            selectManyMethodInfos
                .First(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 3
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>));
        return selectManyMethodInfo;
    }

    public MethodInfo GetIEnumerableDefaultIfEmptyMethodInfo()
    {
        //                                   
        // public static IEnumerable<TSource?> DefaultIfEmpty<TSource>(this IEnumerable<TSource> source);
        IEnumerable<MethodInfo> defaultIfEmptyMethodInfos =
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where(mi => mi.Name == "DefaultIfEmpty");

        MethodInfo defaultIfEmptyMethodInfo =
            defaultIfEmptyMethodInfos
                .First(
                    mi =>
                        mi.IsGenericMethodDefinition
                        && mi.GetParameters().Length == 1);
        return defaultIfEmptyMethodInfo;
    }
}
