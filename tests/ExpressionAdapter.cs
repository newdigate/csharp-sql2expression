using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Reflection;

namespace tests;

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

    public LambdaExpression? CreateExpression(SqlTableExpression expression, out Type elementType) { 
        elementType = null;
        switch (expression) {
            case SqlQualifiedJoinTableExpression sqlJoinStatement: 
                Type mappedOuterType = _typeMapper.GetMappedType(sqlJoinStatement.Right.Sql);
                if (mappedOuterType == null)
                    return null;

                Type mappedInnerType = _typeMapper.GetMappedType(sqlJoinStatement.Left.Sql);
                if (mappedInnerType == null)
                    return null;

                return CreateJoinExpression(sqlJoinStatement, mappedOuterType, mappedInnerType, "o", "i", sqlJoinStatement.OnClause, out elementType);

            case SqlTableRefExpression sqlTableRefStatement: 
                Type mappedType = _typeMapper.GetMappedType(sqlTableRefStatement.Sql);
                if (mappedType == null)
                    return null;
                elementType = mappedType;
                IEnumerable<object> mappedCollection2 = _collectionMapper.GetMappedCollection(sqlTableRefStatement.Sql);
                return CreateRefExpression(sqlTableRefStatement, mappedType, mappedCollection2, "p");
        }
        return null;
    }
    public LambdaExpression? CreateJoinExpression(SqlJoinTableExpression sqlJoinStatement, Type rightMappedType, Type innerMappedType, string outerParameterName, string innerParameterName, SqlConditionClause onClause, out Type elementType) {
        
        elementType = null;
        LambdaExpression right = CreateExpression(sqlJoinStatement.Right, out Type rightElementType);
        LambdaExpression left = CreateExpression(sqlJoinStatement.Left, out Type leftElementType); 

        Type leftKeyType = null;
        Expression outerKeySelector = null;  //Func<TOuter, TKey>
        Expression innerKeySelector = null;  //Func<TInner, TKey>

        IEnumerable<Field> fields = _sqlFieldProvider.GetFields(sqlJoinStatement);

        string dynamicTypeName = $"Dynamic_{sqlJoinStatement.Left.Sql.Replace(".","_")}_{sqlJoinStatement.Right.Sql.Replace(".","_")}";
        Type dynamicType = MyObjectBuilder.CompileResultType(dynamicTypeName, fields);

        Type typeFuncReturnsDynamicType = 
            typeof(Func<>)
                .MakeGenericType(dynamicType);

        ParameterExpression innerParameterExpression = Expression.Parameter(innerMappedType, "inner");
        ParameterExpression outerParameterExpression = Expression.Parameter(rightMappedType, "outer");
        
        List<MemberBinding> bindings = new List<MemberBinding>();
        bindings.Add(Expression.Bind(dynamicType.GetMember(sqlJoinStatement.Left.Sql.ToString().Replace(".","_"))[0], innerParameterExpression));
        bindings.Add(Expression.Bind(dynamicType.GetMember(sqlJoinStatement.Right.Sql.ToString().Replace(".","_"))[0], outerParameterExpression));
                    

        Type typeTupleOfTOuterAndTInner = dynamicType;//.MakeGenericType(rightMappedType, innerMappedType);
        elementType = typeTupleOfTOuterAndTInner;

        LambdaExpression joinSelector = null;

        switch (onClause.Expression){
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:
            
                //Microsoft.SqlServer.Management.SqlParser.SqlCodeDom.SqlScalarRefExpression.SqlColumnOrPropertyRefExpression eeee;
                Expression leftExpression = null;
                
                switch (sqlComparisonBooleanExpression.Left) {
                    case SqlScalarRefExpression innerSqlScalarRefExpression: 
                    {
                        SqlMultipartIdentifier leftsqlMultipartIdentifier = innerSqlScalarRefExpression.MultipartIdentifier;
                        string innertableName = leftsqlMultipartIdentifier.Children.Last().Sql;
                        PropertyInfo leftKeyProperty = innerMappedType.GetProperty(innertableName);
                        leftKeyType = leftKeyProperty.PropertyType;

                        ParameterExpression innerParameterExpression2 = Expression.Parameter(innerMappedType, "inner");
                        MemberExpression innerMemberAccess = Expression.MakeMemberAccess(innerParameterExpression2, leftKeyProperty);
                        innerKeySelector = Expression.Lambda(innerMemberAccess, innerParameterExpression2);
                    }
                    break;
                }
                
                switch (sqlComparisonBooleanExpression.Right) {
                    case SqlScalarRefExpression outerSqlScalarRefExpression: 
                    {
                        SqlMultipartIdentifier outerSqlMultipartIdentifier = outerSqlScalarRefExpression.MultipartIdentifier;
                        string outertableName = outerSqlMultipartIdentifier.Children.Last().Sql;
                        PropertyInfo outerKeyProperty = rightMappedType.GetProperty(outertableName);
                        leftKeyType = outerKeyProperty.PropertyType;

                        ParameterExpression outerParameterExpression2 = Expression.Parameter(rightMappedType, "outer");
                        MemberExpression outerMemberAccess = Expression.MakeMemberAccess(outerParameterExpression2, outerKeyProperty);
                        outerKeySelector = Expression.Lambda(outerMemberAccess, outerParameterExpression2);
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
        Type typeIEnumerableOfTInner = typeof(IEnumerable<>).MakeGenericType(innerMappedType);
        //Func<TOuter, TKey>
        //Type joinKeyType = 
        Type typeFuncTakingTOuterReturningTKey = typeof(Func<,>).MakeGenericType(rightMappedType, leftKeyType);
        Type typeFuncTakingTInnerReturningTKey = typeof(Func<,>).MakeGenericType(innerMappedType, leftKeyType);

        Type typeResultSelector = typeof(Func<,,>).MakeGenericType(rightMappedType, innerMappedType, typeTupleOfTOuterAndTInner);

        //ParameterExpression paramOfTypeIEnumerableOfTOuter = Expression.Parameter(typeIEnumerableOfTOuter, outerParameterName);
        //ParameterExpression paramOfTypeIEnumerableOfTInner = Expression.Parameter(typeIEnumerableOfTInner, innerParameterName);

        ParameterExpression paramOfTypeTOuter = Expression.Parameter(rightMappedType, outerParameterName);
        ParameterExpression paramOfTypeTInner = Expression.Parameter(innerMappedType, innerParameterName);

        //Expression resultSelectorExpression = Expression

        //ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        // Creating an expression for the method call and specifying its parameter.
        //        public static IEnumerable<TResult> Join<TOuter, TInner, TKey, TResult>(this IEnumerable<TOuter> outer, IEnumerable<TInner> inner, Func<TOuter, TKey> outerKeySelector, Func<TInner, TKey> innerKeySelector, Func<TOuter, TInner, TResult> resultSelector);
        

        Type typeIEnumerableOfTuple =
            typeof(IEnumerable<>)
                .MakeGenericType(dynamicType);

        MethodInfo joinSpecificMethodInfo = joinMethodInfo.MakeGenericMethod(new [] { rightMappedType, innerMappedType, leftKeyType, dynamicType });

        MethodCallExpression joinMethodCall = Expression.Call(
            method: joinSpecificMethodInfo,
            instance: null, 
            arguments: new Expression[] {right.Body, left.Body, outerKeySelector, innerKeySelector, joinSelector }
        );

        Type funcTakingNothingAndReturningIEnumerableOfTuple =
            typeof(Func<>)
                .MakeGenericType(typeIEnumerableOfTuple);

        LambdaExpression l2 = Expression.Lambda(
            funcTakingNothingAndReturningIEnumerableOfTuple,
            joinMethodCall, new ParameterExpression[] { });
        return l2;

    }


    public LambdaExpression? CreateRefExpression(SqlTableRefExpression sqlTableRefExpression, Type elementType, IEnumerable<object> elementArray, string parameterName) {
        
        string key = sqlTableRefExpression.Sql;
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
        Expression selectorExpression = Expression.Constant(true);
        switch (whereClause.Expression) {
            case SqlComparisonBooleanExpression sqlComparisonBooleanExpression:
            {
                Expression leftKeySelector = null;
                Type? leftKeyType = null;
                ParameterExpression parameterExpression2 = Expression.Parameter(elementType, "p");

                Expression rightKeySelector = null;
                Type? rightKeyType = null;

                switch (sqlComparisonBooleanExpression.Left) {
                    case SqlScalarRefExpression leftSqlScalarRefExpression: 
                    {
                        GetWhereKeySelectorFromSqlScalarRefExpression(leftSqlScalarRefExpression, elementType, parameterExpression2, out leftKeySelector, out leftKeyType);
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
                        GetWhereKeySelectorFromSqlScalarRefExpression(rightSqlScalarRefExpression, elementType, parameterExpression2, out rightKeySelector, out rightKeyType);
                        break;
                    }

                    case SqlLiteralExpression rightSqlLiteralExpression:
                    {
                        rightKeySelector = Expression.Constant(rightSqlLiteralExpression.Value);
                        break;
                    }
                }

                switch(sqlComparisonBooleanExpression.ComparisonOperator) {
                    case SqlComparisonBooleanExpressionType.Equals: {
                        selectorExpression = Expression.MakeBinary(ExpressionType.Equal, leftKeySelector, rightKeySelector);
                        //typeof(Func<TLeft, bool>)
                        Type typeFuncTakingTElementTypeReturningBool = 
                            typeof(Func<,>)
                                .MakeGenericType(elementType, typeof(bool));
                        LambdaExpression ll = 
                            Expression
                                .Lambda(
                                    typeFuncTakingTElementTypeReturningBool,
                                    selectorExpression,
                                    new ParameterExpression[] { parameterExpression2 }
                                );
                        return ll;
                    }
                }
            }
            break;
        }
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, selectorExpression, selectorParam);

        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression whereMethodCall = Expression.Call(
            method: whereMethodInfo.MakeGenericMethod(new [] { elementType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, selector}
        );

        Type funcTakingIEnumerableOfCustomerReturningIEnumerableOf = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingIEnumerableOfCustomerReturningIEnumerableOf, whereMethodCall, new [] {paramOfTypeIEnumerableOfMappedType});
        return l;
    }

    public LambdaExpression CreateSelectExpression(SqlSelectClause selectClause, Type inputType, string parameterName, out Type? outputType ) {
        outputType = null;

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( inputType ); // == IEnumerable<mappedType>
        Type typeIEnumerableOfObject = typeof(IEnumerable<>).MakeGenericType( typeof(object) ); // == IEnumerable<mappedType>

        //ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);
        ParameterExpression paramOfTypeIEnumerableOfObject = Expression.Parameter(typeIEnumerableOfObject);

        IEnumerable<FieldMapping> fields = _fieldMappingProvider.GetFieldMappings(selectClause, inputType);
        IEnumerable<Field> outputFields = fields.Select( f => new Field() { FieldName = f.OutputFieldName, FieldType=f.FieldType } );
        Type dynamicType = MyObjectBuilder.CompileResultType("Dynamic_"+inputType.Name, outputFields);
        outputType = dynamicType;

        ParameterExpression transformerParam = Expression.Parameter(typeof(object), parameterName);
        Type funcTakingCustomerReturningCustomer = typeof(Func<,>).MakeGenericType( typeof(object), dynamicType);

        List<MemberBinding> bindings = new List<MemberBinding>();
        foreach (FieldMapping f in fields) {
            switch (f.InputFieldName.Count) {
                case 1: {
                    PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                    Expression memberAccess = 
                        Expression.MakeMemberAccess( 
                            Expression.Convert(transformerParam, inputType), 
                            inputProp );
                    bindings.Add(Expression.Bind(dynamicType.GetMember(f.OutputFieldName).First(), memberAccess));
                    break;
                }
                case 2: {
                    PropertyInfo? inputProp = inputType.GetProperty(f.InputFieldName.First().Replace(".", "_"));
                    if (inputProp == null) 
                        continue;
                    Expression memberAccess = 
                        Expression.MakeMemberAccess( 
                            Expression.Convert(transformerParam, inputType), 
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

        //Expression.TypeAs()
        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression selectMethodCall = Expression.Call(
            method: selectMethodInfo.MakeGenericMethod(new [] { typeof(object), dynamicType }),
            instance: null, 
            arguments: 
                new Expression[] { 
                    paramOfTypeIEnumerableOfObject,
                    transformer}
        );

        Type typeIEnumerableOfOutputType = typeof(IEnumerable<>).MakeGenericType( dynamicType ); // == IEnumerable<mappedType>


        //ParameterExpression selectorParam = Expression.Parameter(inputType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(typeIEnumerableOfObject, typeIEnumerableOfOutputType);
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, selectMethodCall, paramOfTypeIEnumerableOfObject);
        return selector;
    }

    public LambdaExpression? CreateExpression(SqlFromClause fromClause, out Type elementType) {
        elementType = null;
        var result = new List<Expression>();
        SqlTableExpression? expression = fromClause.TableExpressions.FirstOrDefault();
        if (expression == null) 
            return null;
        return CreateExpression(expression, out elementType);
    }
}
