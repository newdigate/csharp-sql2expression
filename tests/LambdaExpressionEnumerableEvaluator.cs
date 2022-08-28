using System.Linq.Expressions;

namespace tests;

public class LambdaExpressionEnumerableEvaluator {
    public IEnumerable<object>? Evaluate (LambdaExpression expression){
        Delegate? finalDelegate = expression.Compile();
        if (finalDelegate == null) return null;
        return (IEnumerable<object>?)finalDelegate.DynamicInvoke();
    }
}
