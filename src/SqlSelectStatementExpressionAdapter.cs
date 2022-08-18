using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Reflection;

namespace src;

public class SqlSelectStatementExpressionAdapter {
    private readonly ExpressionAdapter _expressionAdapter;

    public SqlSelectStatementExpressionAdapter(ExpressionAdapter expressionAdapter)
    {
        _expressionAdapter = expressionAdapter;
    }

    public LambdaExpression ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        var query = (SqlQuerySpecification)selectStatement.SelectSpecification.QueryExpression;

        LambdaExpression fromExpression = _expressionAdapter.CreateExpression(query.FromClause, out Type fromExpressionReturnType);
        LambdaExpression whereExpression = _expressionAdapter.CreateWhereExpression(query.WhereClause, fromExpressionReturnType);
        LambdaExpression selectExpression = _expressionAdapter.CreateSelectExpression(query.SelectClause, fromExpressionReturnType, "sss", out Type? outputType);

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( fromExpressionReturnType ); // == IEnumerable<mappedType>

        ParameterExpression selectorParam = Expression.Parameter(fromExpressionReturnType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(fromExpressionReturnType, typeof(bool));
        
        //public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
        IEnumerable<MethodInfo> whereMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Where");

        MethodInfo? whereMethodInfo = 
            whereMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );
               

        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression whereMethodCall = Expression.Call(
            method: whereMethodInfo.MakeGenericMethod(new [] { fromExpressionReturnType }),
            instance: null, 
            arguments: new Expression[] {
                fromExpression.Body, 
                whereExpression}
        );

        Expression finalExpression = 
            Expression
                .Invoke( 
                    selectExpression, 
                    whereMethodCall ); 

        Type typeIEnumerableOfTOutputType = typeof(IEnumerable<>).MakeGenericType( outputType ); // == IEnumerable<mappedType>
        Type typeFuncTakesNothingReturnsIEnumerableOfTOutputType = 
            typeof(Func<>)
                .MakeGenericType(typeIEnumerableOfTOutputType);

        LambdaExpression finalLambda = 
            Expression
                .Lambda(
                    typeFuncTakesNothingReturnsIEnumerableOfTOutputType,
                    finalExpression);
        return finalLambda;
        /*
        WriteLine(fromExpression.ToString());
        WriteLine(whereExpression.ToString());
        WriteLine(selectExpression.ToString());
        */
    }

}