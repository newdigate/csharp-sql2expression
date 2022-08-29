using System.Collections;
using System.Linq.Expressions;

namespace src;

public interface ILambdaExpressionEvaluator
{
    T? Evaluate<T>(LambdaExpression expression);
    IEnumerable? Evaluate(LambdaExpression expression, Type elementType);
}

public class LambdaExpressionEvaluator : ILambdaExpressionEvaluator
{

    public T? Evaluate<T>(LambdaExpression expression)
    {
        Delegate? finalDelegate = expression.Compile();
        if (finalDelegate == null) return default;
        return (T?)finalDelegate.DynamicInvoke();
    }

    public IEnumerable? Evaluate(LambdaExpression expression, Type elementType)
    {
        Delegate? finalDelegate = expression.Compile();
        if (finalDelegate == null) return null;
        return (IEnumerable) finalDelegate.DynamicInvoke();
    }
}
