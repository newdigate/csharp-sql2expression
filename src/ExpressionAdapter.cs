using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace src;

public class ExpressionAdapter : IExpressionAdapter
{
    private readonly ITypeMapper _typeMapper;
    private readonly ICollectionMapper _collectionMapper;
    private readonly ISqlFieldProvider _sqlFieldProvider;
    private readonly IFieldMappingProvider _fieldMappingProvider;
    private readonly IMyObjectBuilder _myObjectBuilder;
    private readonly IEnumerableMethodInfoProvider _ienumerableMethodInfoProvider;
    private readonly ILambdaExpressionEvaluator _lambdaEvaluator;
    private readonly IUniqueNameProviderFactory _uniqueNameProviderFactory;

    public ExpressionAdapter(ITypeMapper typeMapper, ICollectionMapper collectionMapper, ISqlFieldProvider sqlFieldProvider, IFieldMappingProvider fieldMappingProvider, IMyObjectBuilder myObjectBuilder, IEnumerableMethodInfoProvider ienumerableMethodInfoProvider, ILambdaExpressionEvaluator lambdaEvaluator, IUniqueNameProviderFactory uniqueNameProviderFactory)
    {
        _typeMapper = typeMapper;
        _collectionMapper = collectionMapper;
        _sqlFieldProvider = sqlFieldProvider;
        _fieldMappingProvider = fieldMappingProvider;
        _myObjectBuilder = myObjectBuilder;
        _ienumerableMethodInfoProvider = ienumerableMethodInfoProvider;
        _lambdaEvaluator = lambdaEvaluator;
        _uniqueNameProviderFactory = uniqueNameProviderFactory;
    }

    public LambdaExpression? CreateExpression(SqlTableExpression expression, out Type? elementType, Type? projectedOutputType)
    {
        elementType = null;
        switch (expression)
        {
            case SqlQualifiedJoinTableExpression sqlJoinStatement:
                return CreateJoinExpression(sqlJoinStatement, ref projectedOutputType, "o", "i", sqlJoinStatement.OnClause, out elementType);

            case SqlTableRefExpression sqlTableRefStatement:
                string tableRefName = sqlTableRefStatement.ObjectIdentifier.Sql.ToString();
                Type mappedType = _typeMapper.GetMappedType(tableRefName);
                if (mappedType == null)
                    return null;
                elementType = mappedType;
                IEnumerable<object> mappedCollection2 = _collectionMapper.GetMappedCollection(tableRefName);
                return CreateRefExpression(sqlTableRefStatement, mappedType, mappedCollection2, "p");

            case SqlDerivedTableExpression sqlDerivedTableExpression:
                if (sqlDerivedTableExpression.QueryExpression is SqlQuerySpecification sqlQuerySpecification)
                    return ConvertSqlSelectQueryToLambda(sqlQuerySpecification, out elementType);
                break;
        }
        return null;
    }

    public LambdaExpression? ConvertSqlSelectQueryToLambda(SqlQuerySpecification query, out Type? outputType, bool isSqlInConditionBooleanQueryExpression=false)
    {
        LambdaExpression? fromExpression = CreateSourceExpression(query.FromClause, out Type fromExpressionReturnType, out string? tableRefExpressionAlias);
        if (fromExpression == null || fromExpressionReturnType == null)
            throw new ArgumentException($"Translation of from clause failed: '{query.FromClause.Sql}'");

        LambdaExpression? whereExpression = null;
        if (query.WhereClause != null)
            whereExpression = CreateWhereExpression(query.WhereClause, fromExpressionReturnType);

        LambdaExpression? selectExpression = null;
        bool isAggregate = 
            query.SelectClause.SelectExpressions
                .OfType<SqlSelectScalarExpression>()
                .Select( agg => agg.Expression )
                .OfType<SqlAggregateFunctionCallExpression>()
                .Any();

        if (isAggregate) {
            return  CreateAggregateSelectExpression(query, out outputType, fromExpression);
        }
        else if (isSqlInConditionBooleanQueryExpression) {
             selectExpression = CreateSelectScalarExpression(query.SelectClause, fromExpressionReturnType, tableRefExpressionAlias, out outputType);
        } else 
        {
            bool isJustSqlSelctStarExpression = 
                query.SelectClause.SelectExpressions.Count() == 1 && 
                query.SelectClause.SelectExpressions.OfType<SqlSelectStarExpression>().Count() == 1;
            bool isDynamicOutputType = fromExpressionReturnType.GetCustomAttribute<DynamicDataSetElementAttribute>() != null;
            if (isJustSqlSelctStarExpression && !isDynamicOutputType) {
                selectExpression = null;
                outputType = fromExpressionReturnType;
            } else 
                selectExpression = CreateSelectExpression(query.SelectClause, fromExpressionReturnType, tableRefExpressionAlias, out outputType);
        }

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType(fromExpressionReturnType); // == IEnumerable<mappedType>
        ParameterExpression selectorParam = Expression.Parameter(fromExpressionReturnType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(fromExpressionReturnType, typeof(bool));

        MethodCallExpression? whereMethodCall = null;
        if (whereExpression != null)
        {
            MethodInfo? whereMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableWhereMethodInfo();
            // Creating an expression for the method call and specifying its parameter.
            whereMethodCall = Expression.Call(
                method: whereMethodInfo.MakeGenericMethod(new[] { fromExpressionReturnType }),
                instance: null,
                arguments: new Expression[] {
                    fromExpression.Body,
                    whereExpression}
            );
        }
        
        Type typeIEnumerableOfTOutputType = typeof(IEnumerable<>).MakeGenericType(outputType); // == IEnumerable<mappedType>
        Type typeFuncTakesNothingReturnsIEnumerableOfTOutputType =
            typeof(Func<>)
                .MakeGenericType(typeIEnumerableOfTOutputType);

        if (selectExpression == null) {
            return
                Expression
                    .Lambda(
                        typeFuncTakesNothingReturnsIEnumerableOfTOutputType,
                        whereMethodCall??fromExpression.Body);
        }

        MethodInfo? selectMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableSelectMethodInfo();
        MethodCallExpression selectMethodCall = Expression.Call(
            method: selectMethodInfo.MakeGenericMethod(new[] { fromExpressionReturnType, outputType }),
            instance: null,
            arguments: new Expression[] {
                whereMethodCall??fromExpression.Body,
                selectExpression}
        );

        LambdaExpression finalLambda =
            Expression
                .Lambda(
                    typeFuncTakesNothingReturnsIEnumerableOfTOutputType,
                    selectMethodCall);
        return finalLambda;
    }

    private LambdaExpression? CreateAggregateSelectExpression(SqlQuerySpecification query, out Type? outputType, LambdaExpression? fromExpression)
    {
        LambdaExpression? aggregateExpression = null;
        outputType = typeof(int);

        if (query.SelectClause.SelectExpressions.Count() == 1)
        {
            SqlAggregateFunctionCallExpression aggregateFn =
                query.SelectClause.SelectExpressions
                    .OfType<SqlSelectScalarExpression>()
                    .Cast<SqlSelectScalarExpression>()
                    .Select(agg => agg.Expression)
                    .OfType<SqlAggregateFunctionCallExpression>()
                    .First();

            switch (aggregateFn.FunctionName.ToLower())
            {
                case "count":
                    {
                        outputType = typeof(int);
                        MethodInfo countMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableCountMethodInfo();
                        LambdaExpression countExpression =
                            Expression.Lambda(
                                typeof(Func<>).MakeGenericType(outputType),
                                Expression.Call(
                                    instance: null,
                                    method: countMethodInfo.MakeGenericMethod(typeof(object)),
                                    fromExpression.Body)
                            );

                        aggregateExpression = countExpression;
                        break;
                    }
            }
        }

        return aggregateExpression;
    }

    private string GetTypeNameRecursive(SqlTableExpression tableExpression)
    {
        switch (tableExpression)
        {
            case SqlTableRefExpression sqlTableRefExpression: return sqlTableRefExpression.Sql;
            case SqlJoinTableExpression sqlJoinTableExpression:
                return $"{GetTypeNameRecursive(sqlJoinTableExpression.Left)}_{GetTypeNameRecursive(sqlJoinTableExpression.Right)}";
        }
        return "_";
    }

    private string GetOuterTypeNameRecursive(SqlTableExpression tableExpression)
    {
        switch (tableExpression)
        {
            case SqlTableRefExpression sqlTableRefExpression: 
                return sqlTableRefExpression.Sql;
            case SqlJoinTableExpression sqlJoinTableExpression:
                switch(sqlJoinTableExpression.JoinOperator) {
                    case SqlJoinOperatorType.InnerJoin: 
                        return $"{GetOuterTypeNameRecursive(sqlJoinTableExpression.Left)}_{GetOuterTypeNameRecursive(sqlJoinTableExpression.Right)}";
                    case SqlJoinOperatorType.LeftOuterJoin: 
                        return $"{GetOuterTypeNameRecursive(sqlJoinTableExpression.Left)}_{GetOuterTypeNameRecursive(sqlJoinTableExpression.Right)}";
                }
                break;
        }
        return "_";
    }

    private void CreateJoinConditionsExpression(
        SqlScalarRefExpression sqlScalarRefExpression,
        Type innerMappedType,
        Type outerMappedType,
        ParameterExpression innerParameter,
        ParameterExpression outerParameter,
        out Type? innerKeyType,
        out Type? outerKeyType,
        out Expression? innerExpression,
        out Expression? outerExpression)
    {
        innerExpression = null;
        outerExpression = null;

        innerKeyType = null;
        outerKeyType = null;

        SqlMultipartIdentifier innersqlMultipartIdentifier = sqlScalarRefExpression.MultipartIdentifier;
        string lefttableName = innersqlMultipartIdentifier.Children.Last().Sql;

        switch (innersqlMultipartIdentifier.Count)
        {
            case 1:
                {
                    string propertyName = $"{innersqlMultipartIdentifier.First().Sql}";
                    PropertyInfo? innerKeyProperty = innerMappedType.GetProperty(propertyName);
                    if (innerKeyProperty == null)
                    {
                        PropertyInfo? outerKeyProperty = outerMappedType.GetProperty(propertyName);
                        if (outerKeyProperty != null) {
                            outerKeyType = outerKeyProperty.PropertyType;
                            MemberExpression outerMemberAccess = Expression.MakeMemberAccess(outerParameter, outerKeyProperty);
                            outerExpression = Expression.Lambda(outerMemberAccess, false, outerParameter);
                            return;
                        }
                        return;
                    }
                    innerKeyType = innerKeyProperty.PropertyType;
                    MemberExpression innerMemberAccess = Expression.MakeMemberAccess(innerParameter, innerKeyProperty);
                    innerExpression = Expression.Lambda(innerMemberAccess, false, innerParameter);
                    return;
                }
            case 3:
                {
                    string tableName = $"{innersqlMultipartIdentifier.First().Sql}.{innersqlMultipartIdentifier.Skip(1).First().Sql}";
                    string colName = innersqlMultipartIdentifier.Last().Sql;
                    string tablePropName = tableName.Replace(".", "_");
                    
                    Type mappedType = _typeMapper.GetMappedType(tableName);
                    if (mappedType == innerMappedType)
                    {
                        PropertyInfo? innerKeyProperty = innerMappedType.GetProperty(colName);
                        if (innerKeyProperty != null)
                        {
                            innerKeyType = innerKeyProperty.PropertyType;   
                            if (innerParameter.Type.IsGenericType && innerParameter.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
                                ParameterExpression innerElementParameter = Expression.Parameter(innerParameter.Type.GetGenericArguments()[0], "inner");
                                MemberExpression innerMemberAccess2 = Expression.MakeMemberAccess(innerElementParameter, innerKeyProperty);
                                innerExpression = Expression.Lambda(innerMemberAccess2, false, innerElementParameter);
                                return;
                            } 

                            MemberExpression innerMemberAccess = Expression.MakeMemberAccess(innerParameter, innerKeyProperty);
                            innerExpression = Expression.Lambda(innerMemberAccess, false, innerParameter);
                            return;
                        }
                    }
                    else if (mappedType == outerMappedType)
                    {
                        PropertyInfo? outerKeyProperty = outerMappedType.GetProperty(colName);
                        if (outerKeyProperty != null)
                        {
                            outerKeyType = outerKeyProperty.PropertyType;
                            MemberExpression outerMemberAccess = Expression.MakeMemberAccess(outerParameter, outerKeyProperty);
                            outerExpression = Expression.Lambda(outerMemberAccess, outerParameter);
                            return;
                        }
                    }

                    PropertyInfo? joinTableProperty = innerMappedType.GetProperty(tablePropName);
                    if (joinTableProperty != null)
                    {
                        PropertyInfo? outerKeyProperty = joinTableProperty.PropertyType.GetProperty(colName);
                        outerKeyType = outerKeyProperty.PropertyType;
                        outerExpression = Expression.MakeMemberAccess(outerParameter, joinTableProperty);
                        outerExpression = Expression.MakeMemberAccess(outerExpression, outerKeyProperty);
                        outerExpression = Expression.Lambda(outerExpression, outerParameter);
                        return;
                    }

                    joinTableProperty = outerMappedType.GetProperty(tablePropName);
                    if (joinTableProperty != null)
                    {
                        PropertyInfo? outerKeyProperty = joinTableProperty.PropertyType.GetProperty(colName);
                        outerKeyType = outerKeyProperty.PropertyType;
                        outerExpression = Expression.MakeMemberAccess(outerParameter, joinTableProperty);
                        outerExpression = Expression.MakeMemberAccess(outerExpression, outerKeyProperty);
                        outerExpression = Expression.Lambda(outerExpression, outerParameter);
                        return;
                    }              
                    break;
                }
        }
    }
    public LambdaExpression? CreateJoinExpression(SqlJoinTableExpression sqlJoinStatement, ref Type? projectedOutputType, string outerParameterName, string innerParameterName, SqlConditionClause onClause, out Type elementType)
    {
        System.Diagnostics.Debug.WriteLine("CreateJoinExpression:     Left:" + sqlJoinStatement.Left.Sql);
        System.Diagnostics.Debug.WriteLine("CreateJoinExpression:    Right:" + sqlJoinStatement.Right.Sql);
        System.Diagnostics.Debug.WriteLine("CreateJoinExpression: Operator:" + sqlJoinStatement.JoinOperator);

        string leftName = GetOuterTypeNameRecursive(sqlJoinStatement.Left);
        string rightName = GetOuterTypeNameRecursive(sqlJoinStatement.Right);
        string dynamicTypeName = $"{sqlJoinStatement.JoinOperator}_{leftName.Replace(".", "_")}_{rightName.Replace(".", "_")}";
        if (projectedOutputType == null ) {
            // Projected output type contains properties for all the fields of the recursive joins
            IEnumerable<Field> fields = _sqlFieldProvider.GetOuterFields(sqlJoinStatement);
            projectedOutputType = _myObjectBuilder.CompileResultType($"Projected{dynamicTypeName}", fields);
        }

        Type? oneToManyType = null;
        if (sqlJoinStatement.JoinOperator != SqlJoinOperatorType.InnerJoin) {
            IEnumerable<Field> outerFields = _sqlFieldProvider.GetFields(sqlJoinStatement.Left);
            IEnumerable<Field> innerFields = _sqlFieldProvider.GetFields(sqlJoinStatement.Right);
            oneToManyType = _myObjectBuilder.CompileResultType($"Intermediate{dynamicTypeName}", outerFields, innerFields, leftName, rightName);
        } 

        elementType = null;
        Type? outerType = null;
        Type? innerElementType = null;
        Type? innerType = null;

        LambdaExpression? inner = null;
        LambdaExpression? outer = null;
        SqlTableExpression? innerSqlExpression = sqlJoinStatement.Right;
        SqlTableExpression? outerSqlExpression = sqlJoinStatement.Left;

        {
            LambdaExpression? right = CreateExpression(sqlJoinStatement.Right, out Type? rightMappedType, projectedOutputType);
            LambdaExpression? left = CreateExpression(sqlJoinStatement.Left, out Type? leftMappedType, projectedOutputType);
            switch(sqlJoinStatement.JoinOperator) {
                case SqlJoinOperatorType.InnerJoin: {
                    inner = right;
                    innerType = rightMappedType;
                    innerElementType = rightMappedType;
                    outer = left;
                    outerType = leftMappedType;
                    break;
                }
                case SqlJoinOperatorType.LeftOuterJoin: {
                    outer = left;
                    outerType = leftMappedType;

                    inner = right;
                    innerElementType = rightMappedType;
                    innerType = typeof(IEnumerable<>).MakeGenericType(rightMappedType);
                    break;
                }
            }
        }

        if (sqlJoinStatement.JoinOperator == SqlJoinOperatorType.InnerJoin) {
            oneToManyType = projectedOutputType;
        } 

        Type? innerKeyType = null;
        Type? outerKeyType = null;

        Expression? outerKeySelector = null;  //Func<TOuter, TKey>
        Expression? innerKeySelector = null;  //Func<TInner, TKey>

        Type typeFuncReturnsDynamicType =
            typeof(Func<>)
                .MakeGenericType(projectedOutputType);

        ParameterExpression innerParameterExpression = Expression.Parameter(innerType, "inner");
        ParameterExpression outerParameterExpression =  Expression.Parameter(outerType, "outer");
        
        ParameterExpression paramOfTypeTOuter = Expression.Parameter(outerType, outerParameterName);
        ParameterExpression paramOfTypeTInner = Expression.Parameter(innerElementType, innerParameterName);

        Type typeTupleOfTOuterAndTInner = projectedOutputType;
        elementType = typeTupleOfTOuterAndTInner;

        LambdaExpression joinSelector = null;
        switch (onClause.Expression)
        {
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:

                switch (sqlComparisonBooleanExpression.Left)
                {
                    case SqlScalarRefExpression innerSqlScalarRefExpression:
                        {
                            Type? localleftKeyType = null, localrightKeyType = null;
                            Expression? localleftExpression = null, localrightExpression = null;
                            CreateJoinConditionsExpression(innerSqlScalarRefExpression, innerType, outerType, innerParameterExpression, outerParameterExpression, out localleftKeyType, out localrightKeyType, out localleftExpression, out localrightExpression);
                            if (localleftExpression != null)
                            {
                                innerKeySelector = localleftExpression;
                                innerKeyType = localleftKeyType;
                            }
                            else if (localrightExpression != null)
                            {
                                outerKeySelector = localrightExpression;
                                outerKeyType = localrightKeyType;
                            }
                        }
                        break;
                }

                switch (sqlComparisonBooleanExpression.Right)
                {
                    case SqlScalarRefExpression outerSqlScalarRefExpression:
                        {
                            Type? localleftKeyType = null, localrightKeyType = null;
                            Expression? localleftExpression = null, localrightExpression = null;

                            ParameterExpression parameterExpression = 
                                sqlJoinStatement.JoinOperator == SqlJoinOperatorType.InnerJoin? 
                                    outerParameterExpression 
                                    : 
                                    Expression.Parameter(outerType, "j");

                            CreateJoinConditionsExpression(outerSqlScalarRefExpression, innerElementType, outerType, innerParameterExpression, outerParameterExpression, out localleftKeyType, out localrightKeyType, out localleftExpression, out localrightExpression);
                            if (localleftExpression != null)
                            {
                                innerKeySelector = localleftExpression;
                                innerKeyType = localleftKeyType;
                            }
                            else if (localrightExpression != null)
                            {
                                outerKeySelector = localrightExpression;
                                outerKeyType = localrightKeyType;
                            }
                        }
                        break;
                }

                ConstructorInfo? constructorInfo =
                    oneToManyType
                        .GetConstructor(new Type[] { });

                List<MemberBinding> bindings = new List<MemberBinding>();
                IEnumerable<MemberBinding> innerBindings = GetMemberBindingsRecursive(oneToManyType, innerElementType, innerSqlExpression, innerParameterExpression, paramOfTypeTInner);
                IEnumerable<MemberBinding> outerBindings = GetMemberBindingsRecursive(oneToManyType, outerType, outerSqlExpression, outerParameterExpression, null );

                System.Diagnostics.Debug.WriteLine($"Inner bindings: { String.Join(", ", innerBindings.Select(b => b.BindingType + " " + b.Member.ToString()))}");
                System.Diagnostics.Debug.WriteLine($"Outer bindings: { String.Join(", ", outerBindings.Select(b => b.BindingType + " " + b.Member.ToString()))}");
                bindings.AddRange(innerBindings);
                bindings.AddRange(outerBindings);

                Expression testExpr = Expression.MemberInit(
                    Expression.New(constructorInfo, new Expression[] { }),
                    bindings
                );

                joinSelector = Expression.Lambda(testExpr, new[] { outerParameterExpression, innerParameterExpression });
                break;
        }

        MethodInfo? joinMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableJoinMethodInfo();

        Type typeIEnumerableOfTOuter = typeof(IEnumerable<>).MakeGenericType(outerType);
        Type typeIEnumerableOfTInner = typeof(IEnumerable<>).MakeGenericType(innerElementType);
        Type typeFuncTakingTOuterReturningTKey = typeof(Func<,>).MakeGenericType(outerType, outerKeyType);
        Type typeFuncTakingTInnerReturningTKey = typeof(Func<,>).MakeGenericType(innerElementType, innerKeyType);

        bool isOuterJoin = sqlJoinStatement.JoinOperator != SqlJoinOperatorType.InnerJoin;
        Type typeResultSelector 
            = typeof(Func<,,>)
                .MakeGenericType(
                    outerType, 
                    isOuterJoin? 
                        typeof(IEnumerable<>)
                            .MakeGenericType(innerElementType) 
                        : 
                        innerElementType, 
                    typeTupleOfTOuterAndTInner
                    );



        Type typeIEnumerableOfTuple = typeof(IEnumerable<>).MakeGenericType(projectedOutputType);

        switch (sqlJoinStatement.JoinOperator) {
            case SqlJoinOperatorType.InnerJoin: { 
                MethodInfo joinSpecificMethodInfo =
                        joinMethodInfo.MakeGenericMethod(new[] { outerType, innerElementType, innerKeyType, projectedOutputType });

                MethodCallExpression joinMethodCall = Expression.Call(
                    method: joinSpecificMethodInfo,
                    instance: null,
                    arguments:
                        new Expression[] { outer.Body, inner.Body, outerKeySelector, innerKeySelector, joinSelector }
                );

                Type funcTakingNothingAndReturningIEnumerableOfTuple =
                    typeof(Func<>)
                        .MakeGenericType(typeIEnumerableOfTuple);

                LambdaExpression l2 = Expression.Lambda(
                    funcTakingNothingAndReturningIEnumerableOfTuple,
                    joinMethodCall, new ParameterExpression[] { });
                return l2;
            }

            case SqlJoinOperatorType.LeftOuterJoin : {
                MethodInfo groupJoinMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableGroupJoinMethodInfo();
                MethodInfo groupJoinSpecificMethodInfo =
                        groupJoinMethodInfo.MakeGenericMethod(new[] { outerType, innerElementType , innerKeyType, oneToManyType });

                Expression groupJoinCall = 
                    Expression.Call(
                        instance: null,
                        method: groupJoinSpecificMethodInfo,
                        arguments:
                                new Expression[] { outer.Body, inner.Body, outerKeySelector, innerKeySelector, joinSelector }
                            );
                //public static IEnumerable<TResult> SelectMany<TSource, TCollection, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector);
                Type typeFuncTakesTSourceReturnsIEnumerableOfTResult = typeof(Func<,>).MakeGenericType(oneToManyType, typeof(IEnumerable<>).MakeGenericType(innerElementType));
                ParameterExpression collectionSelectorParameter = Expression.Parameter(oneToManyType, "x");
                MemberInfo? innerMemberAccess = oneToManyType.GetProperties().FirstOrDefault( p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>) );
                Expression collectionMemberAccessExpression = 
                    Expression
                        .MakeMemberAccess(collectionSelectorParameter, innerMemberAccess);

                Expression defaultIfEmptyCall = 
                    Expression.Call(
                        instance: null,
                        method: _ienumerableMethodInfoProvider.GetIEnumerableDefaultIfEmptyMethodInfo().MakeGenericMethod(innerElementType),
                        collectionMemberAccessExpression);

                LambdaExpression collectionSelector =
                    Expression.Lambda(
                        typeFuncTakesTSourceReturnsIEnumerableOfTResult,
                        defaultIfEmptyCall,
                        collectionSelectorParameter
                    );

                //    (xinner, xouter) => new {outer = xouter, inner = xinner.customer});
                /*
                Field? leftField = _sqlFieldProvider.GetFields(sqlJoinStatement.Left).FirstOrDefault();
                Field? rightField = _sqlFieldProvider.GetFields(sqlJoinStatement.Right).FirstOrDefault();
                IEnumerable<Field> fields = new [] {leftField, rightField };
                Type flattenedJoinOutputType = _myObjectBuilder.CompileResultType($"Flattened{projectedOutputType.Name}", fields);
                */
                Type typeFuncTakingLHSandRHSReturningTFlattened =
                    typeof(Func<,,>)
                        //.MakeGenericType(projectedOutputType, innerElementType, oneToManyType);
                        .MakeGenericType(oneToManyType, innerElementType, projectedOutputType);

                ParameterExpression outerParameter = Expression.Parameter(projectedOutputType, "oo");
                ParameterExpression innerParameter = Expression.Parameter(innerElementType, "ii");
/*
                PropertyInfo? outputOuterProperty = flattenedJoinOutputType.GetProperty( leftField.FieldName );

                PropertyInfo? outputInnerProperty = flattenedJoinOutputType.GetProperty( rightField.FieldName );
                PropertyInfo? bindingInnerProperty = projectedOutputType.GetProperty( leftField.FieldName);
*/
                List<MemberBinding> bindings = new List<MemberBinding>();
                IEnumerable<MemberBinding> innerBindings = GetMemberManyToOneBindingsRecursive(projectedOutputType, oneToManyType, innerSqlExpression, innerParameterExpression, innerParameter);
                IEnumerable<MemberBinding> outerBindings = GetMemberBindingsRecursive(projectedOutputType, oneToManyType, outerSqlExpression, collectionSelectorParameter, innerParameter);
                bindings.AddRange(outerBindings);
                bindings.AddRange(innerBindings);

                ConstructorInfo? constructorInfo =
                    projectedOutputType
                        .GetConstructor(new Type[] { });

                Expression testExpr = Expression.MemberInit(
                    Expression.New(constructorInfo, new Expression[] { }),
                    bindings
                );

                LambdaExpression resultSelector = 
                    Expression.Lambda(
                        typeFuncTakingLHSandRHSReturningTFlattened,
                        testExpr,
                        new ParameterExpression[] {collectionSelectorParameter, innerParameter}
                        );

                MethodInfo selectMany = _ienumerableMethodInfoProvider.GetIEnumerableSelectManyMethodInfo();
                MethodInfo selectManyGeneric = selectMany.MakeGenericMethod(oneToManyType, innerElementType, projectedOutputType);
                Expression selectManyCall = 
                    Expression.Call(
                        instance: null,
                        method: selectManyGeneric,
                        arguments:
                                new Expression[] { groupJoinCall, collectionSelector, resultSelector }
                            );
                Type funcTakingNothingAndReturningIEnumerableOfTuple =
                    typeof(Func<>)
                        .MakeGenericType(
                            typeof(IEnumerable<>)
                                .MakeGenericType(projectedOutputType));

                LambdaExpression l2 = Expression.Lambda(
                    funcTakingNothingAndReturningIEnumerableOfTuple,
                    selectManyCall, new ParameterExpression[] { });

                elementType = projectedOutputType;
                return l2;
            }
        } 
        return null;
    

    }

    private IEnumerable<MemberBinding> GetMemberBindingsRecursive(Type outputType, Type inputType, SqlTableExpression sqlTableExpression, Expression outputParameterExpression, Expression inputParameterExpression, Expression? innerParameter = null)
    {
        List<MemberBinding> result = new List<MemberBinding>();
        switch (sqlTableExpression)
        {
            case SqlTableRefExpression sqlTableRefExpression:
                {
                    MemberInfo outputMemberInfo = outputType.GetMember(sqlTableRefExpression.Sql.ToString().Replace(".", "_"))[0];
                    if (outputMemberInfo is PropertyInfo outputPropertyInfo)
                    {
                        if (outputParameterExpression.Type == outputPropertyInfo.PropertyType)
                            result.Add(Expression.Bind(outputMemberInfo, outputParameterExpression));
                        else 
                        if (innerParameter != null
                            && outputParameterExpression.Type.IsGenericType 
                            && outputParameterExpression.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) 
                            && outputParameterExpression.Type.GetGenericArguments().First() == outputPropertyInfo.PropertyType )
                        {
                            result.Add(Expression.Bind(outputMemberInfo, innerParameter));
                        }
                        else if (innerParameter != null
                            && innerParameter.Type.IsGenericType 
                            && innerParameter.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) 
                            && innerParameter.Type.GetGenericArguments().First() == outputPropertyInfo.PropertyType )
                        {
                            result.Add(Expression.Bind(outputMemberInfo, innerParameter));
                        }
                        else
                        {
                            PropertyInfo? inputProp = inputType.GetProperty(outputMemberInfo.Name);
                            if (inputProp != null)
                            {
                                if (inputType == outputParameterExpression.Type) {
                                    Expression inputMemberAccess = Expression.MakeMemberAccess(outputParameterExpression, inputProp);
                                    result.Add(Expression.Bind(outputMemberInfo, inputMemberAccess));
                                } 
                            }
                        }
                    }
                    break;
                }
            case SqlJoinTableExpression sqlJoinTableExpression:
                {
                    Expression current = outputParameterExpression;
                    result.AddRange(GetMemberBindingsRecursive(outputType, inputType, sqlJoinTableExpression.Left, outputParameterExpression, inputParameterExpression, innerParameter));
                    result.AddRange(GetMemberBindingsRecursive(outputType, inputType, sqlJoinTableExpression.Right, outputParameterExpression, inputParameterExpression, innerParameter));
                    break;
                }
        }
        return result;
    }

    private IEnumerable<MemberBinding> GetMemberManyToOneBindingsRecursive(Type outputType, Type inputType, SqlTableExpression sqlTableExpression, Expression innerParameterExpression, Expression? innerParameter = null)
    {
        List<MemberBinding> result = new List<MemberBinding>();
        switch (sqlTableExpression)
        {
            case SqlTableRefExpression sqlTableRefExpression:
                {
                    MemberInfo outputMemberInfo = outputType.GetMember(sqlTableRefExpression.Sql.ToString().Replace(".", "_"))[0];
                    if (outputMemberInfo is PropertyInfo outputPropertyInfo)
                    {
                        if (innerParameterExpression.Type == outputPropertyInfo.PropertyType)
                            result.Add(Expression.Bind(outputMemberInfo, innerParameterExpression));
                        else 
                        if (innerParameter != null
                            && innerParameterExpression.Type.IsGenericType 
                            && innerParameterExpression.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) 
                            && innerParameterExpression.Type.GetGenericArguments().First() == outputPropertyInfo.PropertyType )
                        {
                            result.Add(Expression.Bind(outputMemberInfo, innerParameter));
                        }
                        else if (innerParameter != null
                            && innerParameter.Type.IsGenericType 
                            && innerParameter.Type.GetGenericTypeDefinition() == typeof(IEnumerable<>) 
                            && innerParameter.Type.GetGenericArguments().First() == outputPropertyInfo.PropertyType )
                        {
                            result.Add(Expression.Bind(outputMemberInfo, innerParameter));
                        }
                        else
                        {
                            PropertyInfo? inputProp = inputType.GetProperty(outputMemberInfo.Name);
                            if (inputProp != null)
                            {
                                if (inputType == innerParameterExpression.Type) {
                                    Expression inputMemberAccess = Expression.MakeMemberAccess(innerParameterExpression, inputProp);
                                    result.Add(Expression.Bind(outputMemberInfo, inputMemberAccess));
                                } else
                                {
                                    Expression inputMemberAccess = Expression.MakeMemberAccess(innerParameterExpression, inputProp);
                                    result.Add(Expression.Bind(outputMemberInfo, innerParameterExpression));
                                }
                            }
                        }
                    }
                    break;
                }
            case SqlJoinTableExpression sqlJoinTableExpression:
                {
                    result.AddRange(GetMemberBindingsRecursive(outputType, inputType, sqlJoinTableExpression.Left, innerParameterExpression, innerParameter));
                    result.AddRange(GetMemberBindingsRecursive(outputType, inputType, sqlJoinTableExpression.Right, innerParameterExpression, innerParameter));
                    break;
                }
        }
        return result;
    }

    public LambdaExpression? CreateRefExpression(SqlTableRefExpression sqlTableRefExpression, Type elementType, IEnumerable<object> elementArray, string parameterName)
    {
        string key = sqlTableRefExpression.ObjectIdentifier.Sql;
        Type? mappedType = _typeMapper.GetMappedType(key);
        if (mappedType == null)
            return null;

        var constant = Expression.Constant(elementArray);

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType(elementType); // == IEnumerable<mappedType>

        Type funcTakingNothingReturnsIEnumerableOfCustomer = typeof(Func<>).MakeGenericType(typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingNothingReturnsIEnumerableOfCustomer, constant, new ParameterExpression[] { });
        return l;
    }

    public LambdaExpression? CreateWhereExpression(SqlTableRefExpression sqlTableRefExpression, Type mappedType, string parameterName)
    {
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType(mappedType); // == IEnumerable<mappedType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType, parameterName);


        ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(mappedType, typeof(bool));
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, Expression.Constant(true), selectorParam);

        MethodInfo? whereMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableWhereMethodInfo();
           
        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression whereMethodCall = Expression.Call(
            method: whereMethodInfo.MakeGenericMethod(new[] { mappedType }),
            instance: null,
            arguments: new Expression[] { paramOfTypeIEnumerableOfMappedType, selector }
        );

        Type funcTakingIEnumerableOfCustomerReturningIEnumerableOf = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingIEnumerableOfCustomerReturningIEnumerableOf, whereMethodCall, new[] { paramOfTypeIEnumerableOfMappedType });
        return l;
    }

    void GetWhereKeySelectorFromSqlScalarRefExpression(
        SqlScalarRefExpression leftSqlScalarRefExpression,
        Type elementType,
        ParameterExpression parameterExpression2,
        out Expression leftKeySelector,
        out Type? leftKeyType)
    {
        leftKeySelector = null;
        leftKeyType = null;
        SqlMultipartIdentifier leftsqlMultipartIdentifier = leftSqlScalarRefExpression.MultipartIdentifier;
        switch (leftsqlMultipartIdentifier.Count)
        {
            case 1:
                {
                    string leftPropertyName = leftsqlMultipartIdentifier.Children.First().Sql;
                    PropertyInfo mappedProperty = elementType.GetProperty(leftPropertyName);
                    leftKeyType = mappedProperty.PropertyType;

                    leftKeySelector = Expression.MakeMemberAccess(parameterExpression2, mappedProperty);
                    break;
                }
            case 2:
                {
                    string leftPropertyName = leftsqlMultipartIdentifier.Children.Last().Sql;
                    PropertyInfo mappedProperty = elementType.GetProperty(leftPropertyName);
                    leftKeyType = mappedProperty.PropertyType;

                    leftKeySelector = Expression.MakeMemberAccess(parameterExpression2, mappedProperty);
                    break;
                }
            case 3:
                {
                    string leftTableName =
                        leftsqlMultipartIdentifier.Children.First().Sql
                        + "."
                        + leftsqlMultipartIdentifier.Children.Skip(1).First().Sql;

                    string leftPropertyName =
                        leftsqlMultipartIdentifier.Children.First().Sql
                        + "_"
                        + leftsqlMultipartIdentifier.Children.Skip(1).First().Sql;

                    PropertyInfo mappedProperty = elementType.GetProperty(leftPropertyName);
                    Type mappedPropertyType = mappedProperty.PropertyType;

                    string propName = leftsqlMultipartIdentifier.Children.Last().Sql;
                    PropertyInfo leftKeyProperty = mappedPropertyType.GetProperty(propName);
                    leftKeyType = leftKeyProperty.PropertyType;

                    Expression first = Expression.MakeMemberAccess(parameterExpression2, mappedProperty);
                    leftKeySelector = Expression.MakeMemberAccess(first, leftKeyProperty);
                    break;
                }
        }
    }
    private Expression? CreateWhereSelectorExpression(SqlBooleanExpression booleanExpression, Type elementType, ParameterExpression selectorParam)
    {
        Expression? selectorExpression = null;
        switch (booleanExpression)
        {
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:
                {
                    Expression leftKeySelector = null;
                    Type? leftKeyType = null;

                    Expression rightKeySelector = null;
                    Type? rightKeyType = null;

                    switch (sqlComparisonBooleanExpression.Left)
                    {
                        case SqlScalarRefExpression leftSqlScalarRefExpression:
                            {
                                GetWhereKeySelectorFromSqlScalarRefExpression(leftSqlScalarRefExpression, elementType, selectorParam, out leftKeySelector, out leftKeyType);
                            }
                            break;
                        case SqlLiteralExpression leftSqlLiteralExpression:
                            {
                                leftKeySelector = Expression.Constant(leftSqlLiteralExpression.Value);
                                break;
                            }
                    }

                    switch (sqlComparisonBooleanExpression.Right)
                    {
                        case SqlScalarRefExpression rightSqlScalarRefExpression:
                            {
                                GetWhereKeySelectorFromSqlScalarRefExpression(rightSqlScalarRefExpression, elementType, selectorParam, out rightKeySelector, out rightKeyType);
                                break;
                            }

                        case SqlLiteralExpression rightSqlLiteralExpression:
                            {
                                rightKeySelector = rightSqlLiteralExpression.Type == LiteralValueType.Integer ?
                                                Expression.Constant(Convert.ChangeType(rightSqlLiteralExpression.Value, leftKeySelector.Type))
                                                :
                                                Expression.Constant(rightSqlLiteralExpression.Value);
                                break;
                            }
                    }

                    switch (sqlComparisonBooleanExpression.ComparisonOperator)
                    {
                        case SqlComparisonBooleanExpressionType.Equals:
                            {
                                selectorExpression = Expression.MakeBinary(ExpressionType.Equal, leftKeySelector, rightKeySelector);
                                return selectorExpression;
                            }
                    }
                    break;
                }
            case SqlBinaryBooleanExpression sqlBinaryBooleanExpression:
                {
                    Expression? left = CreateWhereSelectorExpression(sqlBinaryBooleanExpression.Left, elementType, selectorParam);
                    Expression? right = CreateWhereSelectorExpression(sqlBinaryBooleanExpression.Right, elementType, selectorParam);
                    ExpressionType? booleanOperator = null;
                    switch (sqlBinaryBooleanExpression.Operator)
                    {
                        case SqlBooleanOperatorType.And: booleanOperator = ExpressionType.AndAlso; break;
                        case SqlBooleanOperatorType.Or: booleanOperator = ExpressionType.OrElse; break;
                    }
                    if (booleanOperator != null)
                        selectorExpression = Expression.MakeBinary(booleanOperator.Value, left, right);
                    return selectorExpression;
                }
            case SqlInBooleanExpression sqlInBooleanExpression:
                {
                    string? propName = null;
                    switch (sqlInBooleanExpression.InExpression)
                    {
                        case SqlColumnRefExpression sqlColumnRefExpression: propName = sqlColumnRefExpression.ColumnName.Sql; break;
                    }
                    if (propName == null)
                        return null;

                    PropertyInfo? propInfo = elementType.GetProperty(propName);
                    if (propInfo == null)
                        return null;

                    ParameterExpression collectionParameter = Expression.Parameter(propInfo.PropertyType, "z");
                    Expression propertyExpression = Expression.MakeMemberAccess(selectorParam, propInfo);
                    Expression equalsExpression = Expression.MakeBinary(ExpressionType.Equal, collectionParameter, propertyExpression);

                    switch (sqlInBooleanExpression.ComparisonValue)
                    {
                        case SqlInBooleanExpressionCollectionValue sqlInBooleanExpressionCollectionValue:

                            List<object> collection = new List<object>();

                            foreach (SqlCodeObject value in sqlInBooleanExpressionCollectionValue.Children)
                            {
                                switch (value)
                                {
                                    case SqlLiteralExpression sqlLiteralExpression:
                                        {
                                            collection.Add(Convert.ChangeType(sqlLiteralExpression.Value, propInfo.PropertyType));
                                        }
                                        break;
                                }
                            }
                            return CreateInStatementFromCollection(propInfo, collectionParameter, equalsExpression, collection);

                        case SqlInBooleanExpressionQueryValue sqlInBooleanExpressionQueryValue:

                            switch (sqlInBooleanExpressionQueryValue.Value) {
                                case SqlQuerySpecification sqlQuerySpecification:
                                    LambdaExpression? expression = 
                                        ConvertSqlSelectQueryToLambda(sqlQuerySpecification, out elementType, true);
                                    
                                    ParameterExpression elementParameter = Expression.Parameter(elementType, "e");
                                    Type memberAccessLambdaType = typeof (Func<,>).MakeGenericType(elementType, propInfo.PropertyType);

                                    bool needToEvaluateQueryableToEnumerable = false;
                                    if (needToEvaluateQueryableToEnumerable) {
                                        IEnumerable? values = _lambdaEvaluator.Evaluate(expression, propInfo.PropertyType);
                                    
                                        List<object> collection2 = new List<object>();
                                        foreach (object value in values)
                                            collection2.Add(Convert.ChangeType(value, propInfo.PropertyType));

                                        return CreateInStatementFromCollection(propInfo, collectionParameter, equalsExpression, collection2);
                                    } else 
                                        return CreateInStatementFromExpression(propInfo, collectionParameter, equalsExpression, expression.Body);
                            }
                            break;

                        case SqlInBooleanExpressionValue sqlScalarExpression: break;

                    }
                    break;
                }
        }
        return selectorExpression;
    }

    private Expression CreateInStatementFromCollection(PropertyInfo? propInfo, ParameterExpression collectionParameter, Expression equalsExpression, List<object> collection)
    {
        Expression c =
            Expression
                .NewArrayInit(
                    propInfo.PropertyType,
                    collection.Select(c => Expression.Constant(Convert.ChangeType(c, propInfo.PropertyType)))
                );
        return CreateInStatementFromExpression(propInfo, collectionParameter, equalsExpression, c);
    }

    private Expression CreateInStatementFromExpression(PropertyInfo? propInfo, ParameterExpression collectionParameter, Expression equalsExpression, Expression collection)
    {
        MethodInfo? anyMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableAnyMethodInfo();
        LambdaExpression ll =
            Expression
                .Lambda(
                    typeof(Func<,>)
                        .MakeGenericType(propInfo.PropertyType, typeof(bool)),
                    equalsExpression,
                    collectionParameter);
        Expression anyMethodCall =
            Expression.Call(
                null,
                anyMethodInfo.MakeGenericMethod(propInfo.PropertyType),
                new Expression[] {
                                    collection,
                                    ll
                    });
        return anyMethodCall;
    }

    public LambdaExpression CreateWhereExpression(SqlWhereClause whereClause, Type elementType)
    {
       // MethodInfo? whereMethodInfo = _ienumerableMethodInfoProvider.GetIEnumerableWhereMethodInfo();
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType(elementType); // == IEnumerable<elementType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);

        ParameterExpression selectorParam = Expression.Parameter(elementType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
        Expression? selectorExpression = CreateWhereSelectorExpression(whereClause.Expression, elementType, selectorParam);

        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, selectorExpression, selectorParam);
        return selector;
    }

    public LambdaExpression CreateSelectExpression(SqlSelectClause selectClause, Type inputType, string parameterName, out Type? outputType)
    {
        outputType = null;

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType(inputType); // == IEnumerable<mappedType>

        ParameterExpression paramOfTypeIEnumerableOfObject = Expression.Parameter(typeIEnumerableOfMappedType, "collection");
        IEnumerable<FieldMapping> fields = _fieldMappingProvider.GetFieldMappings(selectClause, inputType);
        IEnumerable<Field> outputFields = fields.Select(f => new Field() { FieldName = f.OutputFieldName, FieldType = f.FieldType, IsNullable = f.IsNullable });
        Type dynamicType = _myObjectBuilder.CompileResultType("Dynamic_" + inputType.Name, outputFields);
        outputType = dynamicType;

        ParameterExpression transformerParam = Expression.Parameter(inputType, parameterName);
        Type funcTakingCustomerReturningCustomer = typeof(Func<,>).MakeGenericType(inputType, dynamicType);
        List<MemberBinding> bindings = GetBindings(inputType, parameterName, fields, null, dynamicType, transformerParam);

        Expression newDynamicType = Expression.MemberInit(
            Expression.New(dynamicType),
            bindings
        );

        LambdaExpression transformer = Expression.Lambda(funcTakingCustomerReturningCustomer, newDynamicType, transformerParam);
        return transformer;
    }

    private static List<MemberBinding> GetBindings(Type inputType, string parameterName, IEnumerable<FieldMapping> fields, IEnumerable<FieldMapping>? oneToManyFields, Type dynamicType, ParameterExpression transformerParam)
    {
        List<MemberBinding> bindings = new List<MemberBinding>();
        foreach (FieldMapping f in fields)
        {
            bool isTableRefExpressionUsingAnAlias = f.InputFieldName.First() == parameterName;
            switch (f.InputFieldName.Count)
            {
                case 1:
                    {
                        PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                        Expression memberAccess =
                            Expression.MakeMemberAccess(
                                transformerParam,
                                inputProp);
                        bindings.Add(Expression.Bind(dynamicType.GetMember(f.OutputFieldName).First(), memberAccess));
                        break;
                    }
                case 2:
                    {
                        if (isTableRefExpressionUsingAnAlias)
                        {
                            PropertyInfo? inputProp2 = inputType.GetProperty(f.InputFieldName.Last().Replace(".", "_"));
                            Expression memberAccess2 =
                                Expression.MakeMemberAccess(
                                    transformerParam,
                                    inputProp2);
                            bindings.Add(Expression.Bind(dynamicType.GetMember(f.OutputFieldName).First(), memberAccess2));
                            break;
                        }
                        PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                        if (inputProp == null)
                            continue;
                        Expression memberAccess =
                            Expression.MakeMemberAccess(
                                transformerParam,
                                inputProp);

                        bool inputPropertyIsNullable = inputProp.GetCustomAttribute<NullableAttribute>() != null;
                        if (inputPropertyIsNullable) { 
                            if (inputProp.PropertyType.GetProperty(f.InputFieldName.Last()).PropertyType.IsPrimitive) {
                                memberAccess = 
                                    Expression
                                        .Condition(
                                            Expression
                                                .Equal(
                                                    Expression.Constant(null),
                                                    memberAccess),
                                            Expression.Convert(
                                                        Expression.Constant(null),
                                                        typeof(Nullable<>)
                                                            .MakeGenericType(
                                                                inputProp.PropertyType.GetProperty(f.InputFieldName.Last()).PropertyType)
                                                    ),
                                            Expression.Convert(
                                                Expression
                                                    .MakeMemberAccess(
                                                        memberAccess, 
                                                        inputProp.PropertyType.GetProperty(f.InputFieldName.Last())
                                                    ),
                                                    typeof(Nullable<>).MakeGenericType(inputProp.PropertyType.GetProperty(f.InputFieldName.Last()).PropertyType)
                                            )
                                        );
                            } else {
                                memberAccess = 
                                    Expression
                                        .Condition(
                                            Expression
                                                .Equal(
                                                    Expression.Constant(null),
                                                    memberAccess),
                                            Expression.Convert(
                                                        Expression.Constant(null),
                                                        inputProp.PropertyType.GetProperty(f.InputFieldName.Last()).PropertyType
                                                    ),
                                            Expression
                                                .MakeMemberAccess(
                                                    memberAccess, 
                                                    inputProp.PropertyType.GetProperty(f.InputFieldName.Last())
                                                )
                                        );
                            }
                        } 
                        else {
                            inputProp = inputProp.PropertyType.GetProperty(f.InputFieldName.Last());
                            if (inputProp == null)
                                continue;
                            memberAccess = Expression.MakeMemberAccess(memberAccess, inputProp);
                        }
                        bindings.Add(Expression.Bind(dynamicType.GetMember(f.OutputFieldName).First(), memberAccess));

                        break;
                    }

            }
        }

        return bindings;
    }

    public LambdaExpression? CreateSelectScalarExpression(SqlSelectClause selectClause, Type inputType, string parameterName, out Type? outputType)
    {
        outputType = null;
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType(inputType); // == IEnumerable<mappedType>

        ParameterExpression paramOfTypeIEnumerableOfObject = Expression.Parameter(typeIEnumerableOfMappedType, "collection");

        ParameterExpression transformerParam = Expression.Parameter(inputType, parameterName);

        FieldMapping f = 
            _fieldMappingProvider
                .GetFieldMappings(selectClause, inputType)
                .First();

        Expression? memberAccess = null;
        {
            switch (f.InputFieldName.Count)
            {
                case 1:
                    {
                        PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                        outputType = inputProp.PropertyType;
                        memberAccess =
                            Expression.MakeMemberAccess(
                                transformerParam,
                                inputProp);
                        break;
                    }
                case 2:
                    {
                        PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                        if (inputProp == null)
                            return null;

                        memberAccess =
                            Expression.MakeMemberAccess(
                                transformerParam,
                                inputProp);

                        inputProp = inputProp.PropertyType.GetProperty(f.InputFieldName.Last());
                        if (inputProp == null)
                            return null;

                        outputType = inputProp.PropertyType;
                        memberAccess = Expression.MakeMemberAccess(memberAccess, inputProp);
                        break;
                    }

            }
        }
        if (memberAccess == null || outputType == null) 
            return null;

        Type funcTakingCustomerReturningCustomer = typeof(Func<,>).MakeGenericType(inputType, outputType);
        LambdaExpression transformer = Expression.Lambda(funcTakingCustomerReturningCustomer, memberAccess, transformerParam);
        return transformer;
    }


    public LambdaExpression? CreateSourceExpression(SqlFromClause fromClause, out Type? elementType, out string? tableRefExpressionAlias)
    {
        elementType = null;
        tableRefExpressionAlias = null;

        SqlTableExpression? expression = fromClause.TableExpressions.FirstOrDefault();
        if (expression == null) return null;

        return CreateSourceExpression(expression, out elementType, out tableRefExpressionAlias);
    }

    public LambdaExpression? CreateSourceExpression(SqlTableExpression expression, out Type? elementType, out string? tableRefExpressionAlias)
    {
        elementType = null;
        tableRefExpressionAlias = null;
        var result = new List<Expression>();
        if (expression == null)
            return null;
        switch (expression)
        {
            case SqlTableRefExpression sqlTableRefExpression: tableRefExpressionAlias = sqlTableRefExpression.Alias?.Sql; break;
            case SqlDerivedTableExpression sqlDerivedTableExpression: tableRefExpressionAlias = sqlDerivedTableExpression.Alias?.Sql; break;
        }
        return CreateExpression(expression, out elementType, null);
    }
}