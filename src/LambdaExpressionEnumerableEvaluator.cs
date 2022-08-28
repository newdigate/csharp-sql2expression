using System.Collections;
using System.Linq.Expressions;

namespace src;

public interface ILambdaExpressionEnumerableEvaluator
{
    IEnumerable<object>? Evaluate(LambdaExpression expression);
    IEnumerable? Evaluate(LambdaExpression expression, Type elementType);
}

public class LambdaExpressionEnumerableEvaluator : ILambdaExpressionEnumerableEvaluator
{
    public IEnumerable<object>? Evaluate(LambdaExpression expression)
    {
        Delegate? finalDelegate = expression.Compile();
        if (finalDelegate == null) return null;
        return (IEnumerable<object>?)finalDelegate.DynamicInvoke();
    }

    public IEnumerable? Evaluate(LambdaExpression expression, Type elementType)
    {
        Delegate? finalDelegate = expression.Compile();
        if (finalDelegate == null) return null;
        return (IEnumerable) finalDelegate.DynamicInvoke();
    }
}
