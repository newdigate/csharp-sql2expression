using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Reflection;

namespace src;

public class ExpressionAdapter {
    private readonly TypeMapper _typeMapper;
    private readonly CollectionMapper _collectionMapper;

    private readonly SqlFieldProvider _sqlFieldProvider;
    private readonly FieldMappingProvider _fieldMappingProvider;

    public ExpressionAdapter(TypeMapper typeMapper, CollectionMapper collectionMapper, SqlFieldProvider sqlFieldProvider, FieldMappingProvider fieldMappingProvider)
    {
        _typeMapper = typeMapper;
        _collectionMapper = collectionMapper;
        _sqlFieldProvider = sqlFieldProvider;
        _fieldMappingProvider = fieldMappingProvider;
    }

    public LambdaExpression? CreateExpression(SqlTableExpression expression, out Type elementType, Type? joinOutputType) { 
        elementType = null;
        switch (expression) {
            case SqlQualifiedJoinTableExpression sqlJoinStatement: 
                
                if (joinOutputType == null) { 
                    IEnumerable<Field> fields = _sqlFieldProvider.GetFields(sqlJoinStatement);

                    string leftName = GetTypeNameRecursive(sqlJoinStatement.Left);
                    string rightName = GetTypeNameRecursive(sqlJoinStatement.Right);

                    string dynamicTypeName = $"Dynamic_{leftName.Replace(".","_")}_{rightName.Replace(".","_")}";
                    joinOutputType = MyObjectBuilder.CompileResultType(dynamicTypeName, fields);
                }

                return CreateJoinExpression(sqlJoinStatement, joinOutputType, "o", "i", sqlJoinStatement.OnClause, out elementType);

            case SqlTableRefExpression sqlTableRefStatement: 
                string tableRefName = sqlTableRefStatement.ObjectIdentifier.Sql.ToString();
                Type mappedType = _typeMapper.GetMappedType(tableRefName);
                if (mappedType == null)
                    return null;
                elementType = mappedType;
                IEnumerable<object> mappedCollection2 = _collectionMapper.GetMappedCollection(tableRefName);
                return CreateRefExpression(sqlTableRefStatement, mappedType, mappedCollection2, "p");
        }
        return null;
    }

    private string GetTypeNameRecursive(SqlTableExpression tableExpression) {
        switch(tableExpression){
            case SqlTableRefExpression sqlTableRefExpression: return sqlTableRefExpression.Sql;
            case SqlJoinTableExpression sqlJoinTableExpression: 
                return  $"{GetTypeNameRecursive(sqlJoinTableExpression.Left)}_{GetTypeNameRecursive(sqlJoinTableExpression.Right)}";
        }
        return "_";
    }
    private void CreateJoinConditionsExpression(
        SqlScalarRefExpression innerSqlScalarRefExpression, 
        Type leftMappedType, 
        Type rightMappedType, 
        ParameterExpression innerParameterExpression, 
        ParameterExpression rightParameterExpression, 
        out Type? leftKeyType, 
        out Type? rightKeyType, 
        out Expression? leftExpression,
        out Expression? rightExpression)
    {
        leftExpression = null;
        rightExpression = null;
        leftKeyType = null;
        rightKeyType = null;

        Expression innerParameter = innerParameterExpression;
        Expression rightParameter = rightParameterExpression;

        SqlMultipartIdentifier leftsqlMultipartIdentifier = innerSqlScalarRefExpression.MultipartIdentifier;
        string lefttableName = leftsqlMultipartIdentifier.Children.Last().Sql;

        PropertyInfo?   leftKeyProperty = null;
        leftKeyType = null;

        bool conditionLHSRefersToInnerTable = true;
        //bool conditionRHSRefersToInnerTable = true;

        switch (leftsqlMultipartIdentifier.Count) {
            case 1: {
                string propertyName = $"{leftsqlMultipartIdentifier.First().Sql}";
                leftKeyProperty = leftMappedType.GetProperty(propertyName);
                leftKeyType = leftKeyProperty.PropertyType;
                if (leftKeyProperty != null) {
                    conditionLHSRefersToInnerTable = true;
                } else {
                    leftKeyProperty = rightMappedType.GetProperty(propertyName);
                    conditionLHSRefersToInnerTable = !(leftKeyProperty == null); 
                }
                break;
            }
            case 3: {
                string tableName = $"{leftsqlMultipartIdentifier.First().Sql}.{leftsqlMultipartIdentifier.Skip(1).First().Sql}";
                string colName = leftsqlMultipartIdentifier.Last().Sql;
                string tablePropName = tableName.Replace(".", "_");
                Type mappedType = _typeMapper.GetMappedType(tableName);
                if (mappedType == leftMappedType) {
                    leftKeyProperty = leftMappedType.GetProperty(colName);
                    if (leftKeyProperty != null) {
                        leftKeyType = leftKeyProperty.PropertyType;
                    }

                } 
                else if (mappedType == rightMappedType) {
                    PropertyInfo? rightKeyProperty = rightMappedType.GetProperty(colName);
                    if (rightKeyProperty != null) {
                        rightKeyType = rightKeyProperty.PropertyType;
                    } else {
                        PropertyInfo? joinTableProperty = rightMappedType.GetProperty(tablePropName);
                        if (joinTableProperty != null) {
                            rightKeyProperty = joinTableProperty.PropertyType.GetProperty(colName);
                        } else 
                        {
                            joinTableProperty = rightMappedType.GetProperty(tablePropName);
                            conditionLHSRefersToInnerTable = !(joinTableProperty == null); 
                        }

                        if (joinTableProperty != null) {
                            rightKeyProperty = joinTableProperty.PropertyType.GetProperty(colName);
                            rightKeyType = rightKeyProperty.PropertyType;
                        }
                    }
                }
                else {
                    // could be a join dynamic output type
                    PropertyInfo? joinTableProperty = leftMappedType.GetProperty(tablePropName);
                    if (joinTableProperty != null) {
                        leftKeyProperty = joinTableProperty.PropertyType.GetProperty(colName);
                        innerParameter = Expression.MakeMemberAccess( innerParameter, joinTableProperty );
                    } else 
                    {
                        joinTableProperty = rightMappedType.GetProperty(tablePropName);
                        if (joinTableProperty != null) {
                            leftKeyProperty = joinTableProperty.PropertyType.GetProperty(colName);
                            leftKeyType = leftKeyProperty.PropertyType;
                            rightParameter = Expression.MakeMemberAccess( rightParameter, joinTableProperty );
                            // Condition sides are switched?
                        }
                    }
                }
                break;
            }
        }
        if (leftKeyProperty != null) {
                            
            leftKeyType = leftKeyProperty.PropertyType;

            //ParameterExpression innerParameterExpression2 = Expression.Parameter(innerMappedType, "inner");
            MemberExpression innerMemberAccess = Expression.MakeMemberAccess(innerParameter, leftKeyProperty);
            leftExpression = Expression.Lambda(innerMemberAccess, innerParameterExpression);
        } else
        {
            PropertyInfo? rightKeyProperty = rightMappedType.GetProperty(lefttableName); 
            if (rightKeyProperty != null) {
                rightKeyType = rightKeyProperty.PropertyType;
                MemberExpression innerMemberAccess = Expression.MakeMemberAccess(rightParameter, rightKeyProperty);
                rightExpression = Expression.Lambda(innerMemberAccess, rightParameterExpression);
            }
        }
    }
    public LambdaExpression? CreateJoinExpression(SqlJoinTableExpression sqlJoinStatement, Type? joinOutputType, string outerParameterName, string innerParameterName, SqlConditionClause onClause, out Type elementType) {
        
        elementType = null;
        LambdaExpression right = CreateExpression(sqlJoinStatement.Right, out Type rightMappedType, joinOutputType);
        LambdaExpression left = CreateExpression(sqlJoinStatement.Left, out Type leftMappedType, joinOutputType); 

        Type? leftKeyType = null;
        Type? rightKeyType = null;

        Expression? outerKeySelector = null;  //Func<TOuter, TKey>
        Expression? innerKeySelector = null;  //Func<TInner, TKey>

        Type typeFuncReturnsDynamicType = 
            typeof(Func<>)
                .MakeGenericType(joinOutputType);

        ParameterExpression innerParameterExpression = Expression.Parameter(leftMappedType, "left");
        ParameterExpression outerParameterExpression = Expression.Parameter(rightMappedType, "right");
        
        List<MemberBinding> bindings = new List<MemberBinding>();
        bindings.AddRange(GetMemberBindingsRecursive(joinOutputType, leftMappedType, sqlJoinStatement.Left, innerParameterExpression));
        bindings.AddRange(GetMemberBindingsRecursive(joinOutputType, rightMappedType, sqlJoinStatement.Right, outerParameterExpression));

        Type typeTupleOfTOuterAndTInner = joinOutputType;//.MakeGenericType(rightMappedType, innerMappedType);
        elementType = typeTupleOfTOuterAndTInner;

        LambdaExpression joinSelector = null;
        bool conditionSidesSwitched = false;
        switch (onClause.Expression){
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:
                
                switch (sqlComparisonBooleanExpression.Left) {
                    case SqlScalarRefExpression innerSqlScalarRefExpression: 
                    {
                        Type? localleftKeyType = null, localrightKeyType = null; 
                        Expression? localleftExpression = null, localrightExpression = null;
                        CreateJoinConditionsExpression(innerSqlScalarRefExpression, leftMappedType, rightMappedType, innerParameterExpression, outerParameterExpression, out localleftKeyType, out localrightKeyType, out localleftExpression, out localrightExpression);
                        if (localleftExpression != null) {
                            innerKeySelector = localleftExpression;
                            leftKeyType = localleftKeyType;
                        } else if (localrightExpression != null) {
                            outerKeySelector = localrightExpression;
                            rightKeyType = localrightKeyType;
                            conditionSidesSwitched = true;
                        }
                    }
                    break;
                }
                
                switch (sqlComparisonBooleanExpression.Right) {
                    case SqlScalarRefExpression outerSqlScalarRefExpression: 
                    {
                        Type? localleftKeyType = null, localrightKeyType = null; 
                        Expression? localleftExpression = null, localrightExpression = null;
                        CreateJoinConditionsExpression(outerSqlScalarRefExpression, leftMappedType, rightMappedType, innerParameterExpression, outerParameterExpression, out localleftKeyType, out localrightKeyType, out localleftExpression, out localrightExpression);
                        if (localleftExpression != null) {
                            innerKeySelector = localleftExpression;
                            leftKeyType = localleftKeyType;
                        } else if (localrightExpression != null) {
                            outerKeySelector = localrightExpression;
                            rightKeyType = localrightKeyType;
                            conditionSidesSwitched = true;
                        }
                    }
                    break;
                }
            
                ConstructorInfo? constructorInfo = 
                    typeTupleOfTOuterAndTInner
                        .GetConstructor(new Type[] {} );
                
                Expression testExpr = Expression.MemberInit(
                    Expression.New(constructorInfo, new Expression [] { }),
                    bindings
                );

                joinSelector = Expression.Lambda( testExpr, new [] {outerParameterExpression, innerParameterExpression} );
                /*
                SqlMultipartIdentifier leftsqlMultipartIdentifier = leftsqlScalarExpression.MultipartIdentifier;
                string lefttableName = leftsqlMultipartIdentifier.ToString().Replace($".{leftsqlMultipartIdentifier.ColumnOrPropertyName}","");
                
                SqlScalarExpression rightsqlScalarExpression = sqlComparisonBooleanExpression.Right;
                SqlMultipartIdentifier rightsqlMultipartIdentifier = rightsqlScalarExpression.MultipartIdentifier;
                string righttableName = rightsqlMultipartIdentifier.ToString().Replace($".{rightsqlMultipartIdentifier.ColumnOrPropertyName}","");
                
                PropertyInfo leftKeyProperty = innerMappedType.GetProperty(leftsqlMultipartIdentifier.ColumnOrPropertyName);
                PropertyInfo rightKeyProperty = innerMappedType.GetProperty(rightsqlMultipartIdentifier.ColumnOrPropertyName);
*/
               // sqlBooleanExpression.
            break;
        }
       
//        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector);
        IEnumerable<MethodInfo> joinMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Join");

        MethodInfo? joinMethodInfo = 
            joinMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 5 
                        && mi.GetParameters()[4].ParameterType.GetGenericTypeDefinition() == typeof(Func<,,>) );
        
        Type typeIEnumerableOfTOuter = typeof(IEnumerable<>).MakeGenericType(rightMappedType);
        Type typeIEnumerableOfTInner = typeof(IEnumerable<>).MakeGenericType(leftMappedType);
        //Func<TOuter, TKey>
        //Type joinKeyType = 
        Type typeFuncTakingTOuterReturningTKey = typeof(Func<,>).MakeGenericType(rightMappedType, rightKeyType);
        Type typeFuncTakingTInnerReturningTKey = typeof(Func<,>).MakeGenericType(leftMappedType, leftKeyType);

        Type typeResultSelector = typeof(Func<,,>).MakeGenericType(leftMappedType, rightMappedType, typeTupleOfTOuterAndTInner);

        //ParameterExpression paramOfTypeIEnumerableOfTOuter = Expression.Parameter(typeIEnumerableOfTOuter, outerParameterName);
        //ParameterExpression paramOfTypeIEnumerableOfTInner = Expression.Parameter(typeIEnumerableOfTInner, innerParameterName);

        ParameterExpression paramOfTypeTOuter = Expression.Parameter(rightMappedType, outerParameterName);
        ParameterExpression paramOfTypeTInner = Expression.Parameter(leftMappedType, innerParameterName);

        //Expression resultSelectorExpression = Expression

        //ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        // Creating an expression for the method call and specifying its parameter.
        //        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector);
        

        Type typeIEnumerableOfTuple =
            typeof(IEnumerable<>)
                .MakeGenericType(joinOutputType);

        MethodInfo joinSpecificMethodInfo = 
            conditionSidesSwitched?
                joinMethodInfo.MakeGenericMethod(new [] { rightMappedType, leftMappedType, leftKeyType, joinOutputType })
                :
                joinMethodInfo.MakeGenericMethod(new [] { leftMappedType, rightMappedType, leftKeyType, joinOutputType });

        MethodCallExpression joinMethodCall = Expression.Call(
            method: joinSpecificMethodInfo,
            instance: null, 
            arguments: 
            conditionSidesSwitched?
                new Expression[] {right.Body, left.Body, outerKeySelector, innerKeySelector, joinSelector }
                :
                new Expression[] {left.Body, right.Body, innerKeySelector, outerKeySelector, joinSelector }
        );

        Type funcTakingNothingAndReturningIEnumerableOfTuple =
            typeof(Func<>)
                .MakeGenericType(typeIEnumerableOfTuple);

        LambdaExpression l2 = Expression.Lambda(
            funcTakingNothingAndReturningIEnumerableOfTuple,
            joinMethodCall, new ParameterExpression[] { });
        return l2;

    }

    private IEnumerable<MemberBinding> GetMemberBindingsRecursive(Type outputType, Type inputType, SqlTableExpression sqlTableExpression, Expression parameterExpression)
    {
        List<MemberBinding> result = new List<MemberBinding>();
        switch(sqlTableExpression) {
            case SqlTableRefExpression sqlTableRefExpression: {
                MemberInfo outputMemberInfo = outputType.GetMember(sqlTableRefExpression.Sql.ToString().Replace(".","_"))[0];
                if (outputMemberInfo is PropertyInfo outputPropertyInfo) {
                    if (parameterExpression.Type == outputPropertyInfo.PropertyType)
                        result.Add(Expression.Bind(outputMemberInfo, parameterExpression));
                    else {
                        PropertyInfo? inputProp = inputType.GetProperty(outputMemberInfo.Name);
                        if (inputProp != null) {
                            Expression inputMemberAccess = Expression.MakeMemberAccess(parameterExpression, inputProp );
                            result.Add(Expression.Bind(outputMemberInfo, inputMemberAccess));
                        }
                    }
                }
                break;
            }
            case SqlJoinTableExpression sqlJoinTableExpression: {
                Expression current = parameterExpression;
                result.AddRange(GetMemberBindingsRecursive(outputType, inputType, sqlJoinTableExpression.Left, current));
                result.AddRange(GetMemberBindingsRecursive(outputType, inputType, sqlJoinTableExpression.Right, current));
                break;
            }
        }
        return result;
    }

    public LambdaExpression? CreateRefExpression(SqlTableRefExpression sqlTableRefExpression, Type elementType, IEnumerable<object> elementArray, string parameterName) {
        
        string key = sqlTableRefExpression.ObjectIdentifier.Sql;
        Type? mappedType = _typeMapper.GetMappedType(key);
        if (mappedType == null)
            return null;

        var constant = Expression.Constant(elementArray);
        
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( elementType ); // == IEnumerable<mappedType>

        Type funcTakingNothingReturnsIEnumerableOfCustomer = typeof(Func<>).MakeGenericType(typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingNothingReturnsIEnumerableOfCustomer, constant, new ParameterExpression[] {});
        return l;
    }

    public LambdaExpression? CreateWhereExpression(SqlTableRefExpression sqlTableRefExpression, Type mappedType, string parameterName) {
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( mappedType ); // == IEnumerable<mappedType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType, parameterName);


        ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(mappedType, typeof(bool));
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, Expression.Constant(true), selectorParam);
        
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
            method: whereMethodInfo.MakeGenericMethod(new [] { mappedType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, selector}
        );

        Type funcTakingIEnumerableOfCustomerReturningIEnumerableOf = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingIEnumerableOfCustomerReturningIEnumerableOf, whereMethodCall, new [] {paramOfTypeIEnumerableOfMappedType});
        return l;
        //Func<IEnumerable<Customer>, IEnumerable<Customer>> transform = (Func<IEnumerable<Customer>, IEnumerable<Customer>>)l.Compile();
        //IEnumerable<Customer> transformed = transform(mappedCollection.OfType<Customer>().Cast<Customer>());

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
        switch (leftsqlMultipartIdentifier.Count) {
            case 1 : {
                string leftPropertyName =  leftsqlMultipartIdentifier.Children.First().Sql;
                PropertyInfo mappedProperty = elementType.GetProperty(leftPropertyName);
                leftKeyType = mappedProperty.PropertyType;

                leftKeySelector = Expression.MakeMemberAccess(parameterExpression2, mappedProperty);                                
                break;
            }
            case 2 : {
                string leftPropertyName =  leftsqlMultipartIdentifier.Children.Last().Sql;
                PropertyInfo mappedProperty = elementType.GetProperty(leftPropertyName);
                leftKeyType = mappedProperty.PropertyType;
                                
                leftKeySelector = Expression.MakeMemberAccess(parameterExpression2, mappedProperty);                                
                break;
            }
            case 3 : {
                string leftTableName = 
                    leftsqlMultipartIdentifier.Children.First().Sql
                    + "."
                    + leftsqlMultipartIdentifier.Children.Skip(1).First().Sql;
                string leftPropertyName = 
                    leftsqlMultipartIdentifier.Children.First().Sql
                    + "_"
                    + leftsqlMultipartIdentifier.Children.Skip(1).First().Sql;                     
                //Type mappedType = GetMappedType(leftTableName);
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
    private Expression? CreateWhereSelectorExpression(SqlBooleanExpression booleanExpression, Type elementType, ParameterExpression selectorParam) {
        Expression? selectorExpression = null;
        switch (booleanExpression) {
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:
            {
                Expression leftKeySelector = null;
                Type? leftKeyType = null;

                Expression rightKeySelector = null;
                Type? rightKeyType = null;

                switch (sqlComparisonBooleanExpression.Left) {
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
                
                switch (sqlComparisonBooleanExpression.Right) {
                    case SqlScalarRefExpression rightSqlScalarRefExpression: 
                    {
                        GetWhereKeySelectorFromSqlScalarRefExpression(rightSqlScalarRefExpression, elementType, selectorParam, out rightKeySelector, out rightKeyType);
                        break;
                    }

                    case SqlLiteralExpression rightSqlLiteralExpression:
                    {
                        rightKeySelector = rightSqlLiteralExpression.Type == LiteralValueType.Integer?
                                        Expression.Constant(Convert.ChangeType(rightSqlLiteralExpression.Value, leftKeySelector.Type))
                                        :
                                        Expression.Constant(rightSqlLiteralExpression.Value);
                        break;
                    }
                }

                switch(sqlComparisonBooleanExpression.ComparisonOperator) {
                    case SqlComparisonBooleanExpressionType.Equals: {
                        selectorExpression = Expression.MakeBinary(ExpressionType.Equal, leftKeySelector, rightKeySelector);
                        return selectorExpression;

                    }
                }
                break;
            }
            case SqlBinaryBooleanExpression sqlBinaryBooleanExpression: {
                Expression? left = CreateWhereSelectorExpression(sqlBinaryBooleanExpression.Left, elementType, selectorParam);
                Expression? right = CreateWhereSelectorExpression(sqlBinaryBooleanExpression.Right, elementType, selectorParam);
                ExpressionType? booleanOperator = null;
                switch(sqlBinaryBooleanExpression.Operator) {
                    case SqlBooleanOperatorType.And: booleanOperator = ExpressionType.And; break;
                    case SqlBooleanOperatorType.Or:  booleanOperator = ExpressionType.Or; break;

                }
                if (booleanOperator != null)
                    selectorExpression = Expression.MakeBinary(booleanOperator.Value, left, right);
                return selectorExpression;
            }
        }
        return selectorExpression;
    }
    public LambdaExpression CreateWhereExpression(SqlWhereClause whereClause, Type elementType) {
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
        
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( elementType ); // == IEnumerable<elementType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);

        ParameterExpression selectorParam = Expression.Parameter(elementType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(elementType, typeof(bool));
        Expression? selectorExpression = CreateWhereSelectorExpression(whereClause.Expression, elementType, selectorParam);
        


        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, selectorExpression, selectorParam);
        return selector;
    }

    public LambdaExpression CreateSelectExpression(SqlSelectClause selectClause, Type inputType, string parameterName, out Type? outputType ) {
        outputType = null;

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( inputType ); // == IEnumerable<mappedType>
        //Type typeIEnumerableOfObject = typeof(IEnumerable<>).MakeGenericType( typeof(object) ); // == IEnumerable<mappedType>

        //ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);
        ParameterExpression paramOfTypeIEnumerableOfObject = Expression.Parameter(typeIEnumerableOfMappedType, "collection");
        IEnumerable<FieldMapping> fields = _fieldMappingProvider.GetFieldMappings(selectClause, inputType);
        IEnumerable<Field> outputFields = fields.Select( f => new Field() { FieldName = f.OutputFieldName, FieldType=f.FieldType } );
        Type dynamicType = MyObjectBuilder.CompileResultType("Dynamic_"+inputType.Name, outputFields);
        outputType = dynamicType;

        ParameterExpression transformerParam = Expression.Parameter(inputType, parameterName);
        Type funcTakingCustomerReturningCustomer = typeof(Func<,>).MakeGenericType( inputType, dynamicType);
        

        List<MemberBinding> bindings = new List<MemberBinding>();
        foreach (FieldMapping f in fields) {
            bool isTableRefExpressionUsingAnAlias = f.InputFieldName.First() == parameterName;
            switch (f.InputFieldName.Count) {
                case 1: {
                    PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                    Expression memberAccess = 
                        Expression.MakeMemberAccess( 
                            transformerParam,
                            inputProp );
                    bindings.Add(Expression.Bind(dynamicType.GetMember(f.OutputFieldName).First(), memberAccess));
                    break;
                }
                case 2: {
                    if (isTableRefExpressionUsingAnAlias) {
                        PropertyInfo? inputProp2 = inputType.GetProperty(f.InputFieldName.Last().Replace(".", "_"));
                        Expression memberAccess2 = 
                            Expression.MakeMemberAccess( 
                                transformerParam,
                                inputProp2 );
                        bindings.Add(Expression.Bind(dynamicType.GetMember(f.OutputFieldName).First(), memberAccess2));
                        break;
                    }
                    PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                    if (inputProp == null) 
                        continue;
                    Expression memberAccess = 
                        Expression.MakeMemberAccess( 
                            transformerParam, 
                            inputProp );

                    inputProp = inputProp.PropertyType.GetProperty(f.InputFieldName.Last());
                    if (inputProp == null) 
                        continue;
                    memberAccess = Expression.MakeMemberAccess( memberAccess, inputProp );

                    bindings.Add(Expression.Bind(dynamicType.GetMember(f.OutputFieldName).First(), memberAccess));

                    break;
                }

            }
        }

        Expression newDynamicType = Expression.MemberInit(
            Expression.New(dynamicType), 
            bindings
        );

        LambdaExpression transformer = Expression.Lambda(funcTakingCustomerReturningCustomer, newDynamicType, transformerParam);
        return transformer;
        /*
        IEnumerable<MethodInfo> selectMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Select");

        MethodInfo? selectMethodInfo = 
            selectMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );

        */
        /* 
        IEnumerable<MethodInfo> castMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Cast");

        MethodInfo? castMethodInfo = 
            castMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 1
                        );

        //public static IEnumerable<TResult> Cast<TResult>(this IEnumerable source);
        //System.Linq.Enumerable.Cast<int>()
        MethodCallExpression castMethodCall = Expression.Call(
            method: castMethodInfo.MakeGenericMethod(new [] { inputType }),
            instance: null, 
            arguments: 
                new Expression[] { 
                    paramOfTypeIEnumerableOfObject 
                    }
        );
        */
        //Expression.TypeAs()
        // Creating an expression for the method call and specifying its parameter.
        /*
        MethodCallExpression selectMethodCall = Expression.Call(
            method: selectMethodInfo.MakeGenericMethod(new [] { inputType, dynamicType }),
            instance: null, 
            arguments: 
                new Expression[] { 
                    //castMethodCall,
                    paramOfTypeIEnumerableOfObject,
                    transformer}
        );

        Type typeIEnumerableOfOutputType = typeof(IEnumerable<>).MakeGenericType( dynamicType ); // == IEnumerable<mappedType>


        //ParameterExpression selectorParam = Expression.Parameter(inputType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfOutputType);

        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, selectMethodCall, paramOfTypeIEnumerableOfObject );
        return selector;
        */
    }

    public LambdaExpression? CreateSourceExpression(SqlFromClause fromClause, out Type elementType, out string? tableRefExpressionAlias) {
        elementType = null;
        tableRefExpressionAlias = null;
        var result = new List<Expression>();
        SqlTableExpression? expression = fromClause.TableExpressions.FirstOrDefault();
        if (expression == null) 
            return null;
        if (expression is SqlTableRefExpression sqlTableRefExpression) {
            tableRefExpressionAlias = sqlTableRefExpression.Alias?.Sql;
        }
        return CreateExpression(expression, out elementType, null);
    }
}
