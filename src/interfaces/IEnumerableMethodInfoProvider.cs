using System.Reflection;

namespace src;

public interface IEnumerableMethodInfoProvider
{
    MethodInfo? GetIEnumerableAnyMethodInfo();
    MethodInfo? GetIEnumerableJoinMethodInfo();
    MethodInfo? GetIEnumerableSelectMethodInfo();
    MethodInfo? GetIEnumerableWhereMethodInfo();
}
