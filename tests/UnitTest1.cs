using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;

public class UnitTest1
{
    public class Customer {
        public int Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public int CategoryId { get; set; }
    }

    public class Category {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class SqlPropertyRefColumn {
        private readonly PropertyInfo _property;
        private readonly SqlTableRefExpression _sqlTableRefExpression;
        private readonly Type _mappedType;

        public SqlPropertyRefColumn(PropertyInfo property, SqlTableRefExpression sqlTableRefExpression, Type mappedType)
        {
            _property = property;
            _sqlTableRefExpression = sqlTableRefExpression;
            _mappedType = mappedType;
        }

        public PropertyInfo Property { get { return _property; } }
        public SqlTableRefExpression SqlTableRefExpression { get { return _sqlTableRefExpression; } }
        public Type MappedType { get { return _mappedType; } }

    }

    private static readonly Customer[] _customers = new [] { new Customer() {Id = 1, Name="Nic", State="MA", CategoryId=1}};
    private static readonly Category[] _categories = new [] { new Category() {Id = 1, Name="Tier 1"}};

 
    private readonly Dictionary<string, IEnumerable<object>> _map = 
        new Dictionary<string, IEnumerable<object>>{
            { "dbo.Customers", _customers},
            { "dbo.Categories", _categories}};



    [Fact]
    public void Test1()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE State = 'MA'";
        var parseResult = Parser.Parse(sql);
        foreach (var batch in parseResult.Script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                switch (statement)
                {
                    case SqlSelectStatement selectStatement:
                        ProcessSelectStatement(selectStatement);
                        break;
                    default:
                        WriteLine("Unsupported statment. Printing inner XML");
                        WriteLine(statement.Xml);
                        break;
                }
            }
        }
    }

    [Fact]
    public void Test2()
    {
        const string sql = @"
SELECT 
    dbo.Customers.Id, 
    dbo.Customers.Name, 
    dbo.Categories.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories 
    ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.State = 'MA'";
        var parseResult = Parser.Parse(sql);

        foreach (var batch in parseResult.Script.Batches)
        {
            foreach (var statement in batch.Statements)
            {
                switch (statement)
                {
                    case SqlSelectStatement selectStatement:
                        ProcessSelectStatement(selectStatement);
                        break;
                    default:
                        WriteLine("Unsupported statment. Printing inner XML");
                        WriteLine(statement.Xml);
                        break;
                }
            }
        }
    }

    List<SqlPropertyRefColumn> GetSchema(SqlFromClause clause) {
        var result = new List<SqlPropertyRefColumn>();
        foreach(SqlTableExpression expression in clause.TableExpressions) {
            result.AddRange(GetSqlTableExpressionSchema(expression));
        }
        return result;
    }

    LambdaExpression CreateExpression(SqlTableExpression expression, Type elementType, IEnumerable<object> elements, string parameterName) {
        switch (expression) {
            case SqlJoinTableExpression sqlJoinStatement: 
                return null;// CreateJoinExpression(sqlJoinStatement, );
                break;
            case SqlTableRefExpression sqlTableRefStatement: 
                return CreateRefExpression(sqlTableRefStatement, elementType, elements, parameterName );
                break;
        }
        return null;
    }

    
    IEnumerable<Field> GetFields(SqlJoinTableExpression sqlJoinStatement) {
        List<Field> result = new List<Field>();
        result.AddRange(GetFields(sqlJoinStatement.Left));
        result.AddRange(GetFields(sqlJoinStatement.Right));
        return result;
    }


    IEnumerable<Field> GetFields(SqlTableExpression sqlTableExpression) {
        List<Field> result = new List<Field>();
        switch (sqlTableExpression)
        {
            case SqlJoinTableExpression sqlJoinTableExpression: 
            {
                result.AddRange(GetFields(sqlJoinTableExpression));
            }
            break;
            case SqlTableRefExpression sqlTableRefExpression: 
            {
                result.AddRange(GetFields(sqlTableRefExpression));
            }
            break;
        }
        return result;
    }

    IEnumerable<Field> GetFields(SqlTableRefExpression sqlTableRefExpression) {
        List<Field> result = new List<Field>();

        Type? mappedType = GetMappedType(sqlTableRefExpression.Sql);
        if (mappedType == null)
            return result;

        Field f = new Field() { FieldName = sqlTableRefExpression.Sql.ToString().Replace(".","_"), FieldType = mappedType};
        result.Add(f);
        return result;
    }

    IEnumerable<Field> GetFields(SqlSelectClause selectClause, Type inputType) {
        List<Field> result = new List<Field>();
        foreach (SqlSelectExpression sqlSelectExpression in selectClause.SelectExpressions) {

            switch (sqlSelectExpression) {
                case SqlSelectScalarExpression sqlSelectScalarExpression :
                {
                    switch(sqlSelectScalarExpression.Expression) {
                        case SqlScalarRefExpression sqlSelectScalarRefExpression: {
                            SqlMultipartIdentifier m = sqlSelectScalarRefExpression.MultipartIdentifier;

                            switch (m.Count) {
                                case 1: break;
                                case 2: break;
                                case 3: {
                                    string typeName = $"{m.First().Sql}.{m.Skip(1).First().Sql}";
                                    string colName = m.Last().Sql;

                                    Type mappedType = GetMappedType(typeName);

                                    PropertyInfo propInfo = mappedType.GetProperty(colName);
                            
                                    Field f = new Field() 
                                    {
                                        FieldName = m.ToString().Replace(".","_"),
                                        FieldType = propInfo.PropertyType
                                    };
                                    result.Add(f);
                                    break;
                                }
                            }



                            break;
                        }
                    }

                    break;
                }
            }


        }

        //result.AddRange(GetFields(sqlJoinStatement.Left));
        return result;
    }
    IEnumerable<FieldMapping> GetFieldMappings(SqlSelectClause selectClause, Type inputType) {
        
        List<FieldMapping> result = new List<FieldMapping>();
        foreach (SqlSelectExpression sqlSelectExpression in selectClause.SelectExpressions) {

            switch (sqlSelectExpression) {
                case SqlSelectScalarExpression sqlSelectScalarExpression :
                {
                    switch(sqlSelectScalarExpression.Expression) {
                        case SqlScalarRefExpression sqlSelectScalarRefExpression: {
                            SqlMultipartIdentifier m = sqlSelectScalarRefExpression.MultipartIdentifier;
                            
                            switch (m.Count) {
                                case 1: {
                                    string propertyName = $"{m.First().Sql}";
                                    PropertyInfo propInfo = inputType.GetProperty(propertyName);
                                    FieldMapping f = new FieldMapping() 
                                    {
                                        InputFieldName = new List<string>() {propertyName },
                                        OutputFieldName = m.ToString().Replace(".","_"),
                                        FieldType = propInfo.PropertyType
                                    };
                                    result.Add(f);
                                    break;
                                }
                                case 3: {
                                    string typeName = $"{m.First().Sql}.{m.Skip(1).First().Sql}";
                                    string colName = m.Last().Sql;
                                    
                                    Type? mappedType = GetMappedType(typeName);
                                    if (mappedType == null)
                                        break;

                                    PropertyInfo propInfo = mappedType.GetProperty(colName);
                                    FieldMapping f = new FieldMapping() 
                                    {
                                        InputFieldName = new List<string>() {typeName, colName},
                                        OutputFieldName = m.ToString().Replace(".","_"),
                                        FieldType = propInfo.PropertyType
                                    };
                                    result.Add(f);
                                    break;
                                }
                            }

                            
                            break;
                        }
                    }

                    break;
                }
            }


        }

        //result.AddRange(GetFields(sqlJoinStatement.Left));
        return result;
    }

    LambdaExpression? CreateJoinExpression(SqlJoinTableExpression sqlJoinStatement, Type rightMappedType, Type innerMappedType, string outerParameterName, string innerParameterName, SqlConditionClause onClause, out Type elementType) {
        
        elementType = null;
        LambdaExpression right = CreateExpression(sqlJoinStatement.Right, out Type rightElementType);
        LambdaExpression left = CreateExpression(sqlJoinStatement.Left, out Type leftElementType); 

        Type leftKeyType = null;
        Expression outerKeySelector = null;  //Func<TOuter, TKey>
        Expression innerKeySelector = null;  //Func<TInner, TKey>
        var ssds = () => new {};



        IEnumerable<Field> fields = GetFields(sqlJoinStatement);

        //PropertyInfo wak = new PropertyInfo() {Name = "wak", PropertyType=typeof(string) };
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

    Type? GetMappedType(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key].GetType().GetElementType();
        }
        return null;
    }
    IEnumerable<object>? GetMappedCollection(string key) {
        if (_map.ContainsKey(key)) {
            return _map[key];
        }
        return null; 
    }
    LambdaExpression? CreateRefExpression(SqlTableRefExpression sqlTableRefExpression, Type elementType, IEnumerable<object> elementArray, string parameterName) {
        
        //Expression<Func<IEnumerable<Customer>>> customersss = () => elementArray;

        string key = sqlTableRefExpression.Sql;
        Type? mappedType = GetMappedType(key);
        if (mappedType == null)
            return null;


        var constant = Expression.Constant(elementArray);
        
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( elementType ); // == IEnumerable<mappedType>

        Type funcTakingNothingReturnsIEnumerableOfCustomer = typeof(Func<>).MakeGenericType(typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingNothingReturnsIEnumerableOfCustomer, constant, new ParameterExpression[] {});
        return l;
    }

    LambdaExpression? CreateWhereExpression(SqlTableRefExpression sqlTableRefExpression, Type mappedType, string parameterName) {
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



    LambdaExpression CreateSelectExpression(SqlSelectClause selectClause, Type inputType, string parameterName, out Type? outputType ) {
        outputType = null;

        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( inputType ); // == IEnumerable<mappedType>
        Type typeIEnumerableOfObject = typeof(IEnumerable<>).MakeGenericType( typeof(object) ); // == IEnumerable<mappedType>

        //ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);
        ParameterExpression paramOfTypeIEnumerableOfObject = Expression.Parameter(typeIEnumerableOfObject);

        IEnumerable<FieldMapping> fields = GetFieldMappings(selectClause, inputType);
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

    #region column stuff
    List<SqlPropertyRefColumn> GetSqlTableExpressionSchema(SqlTableExpression expression) {
        var result = new List<SqlPropertyRefColumn>();
        switch (expression) {
            case SqlJoinTableExpression sqlJoinStatement: 
                result.AddRange( GetJoinSchema(sqlJoinStatement) );
                break;
            case SqlTableRefExpression sqlTableRefStatement: 
                result.AddRange( GetSqlTableRefExpressionSchema(sqlTableRefStatement) );
                break;
        }
        return result;
    }

    List<SqlPropertyRefColumn> GetJoinSchema(SqlJoinTableExpression sqlJoinStatement) {
        var result = new List<SqlPropertyRefColumn>();
        result.AddRange(GetSqlTableExpressionSchema(sqlJoinStatement.Left));
        result.AddRange(GetSqlTableExpressionSchema(sqlJoinStatement.Right));
        return result;
    }

    List<SqlPropertyRefColumn> GetSqlTableRefExpressionSchema(SqlTableRefExpression sqlTableRefExpression) {
        Type? mappedType = null;
        if (_map.ContainsKey(sqlTableRefExpression.Sql)) {
            mappedType = _map[sqlTableRefExpression.Sql].GetType().GetElementType();
            return 
                mappedType
                    .GetProperties()
                    .Select( 
                        p => 
                            new SqlPropertyRefColumn(p, sqlTableRefExpression, mappedType ))
                    .ToList();
        }
        return new List<SqlPropertyRefColumn>();
    }
    #endregion

    public static object GetDefault(Type type)
    {
        if(type.GetTypeInfo().IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        return null;
    }
    
    public LambdaExpression? CreateExpression(SqlTableExpression expression, out Type elementType) { 
        elementType = null;
        switch (expression) {
            case SqlQualifiedJoinTableExpression sqlJoinStatement: 
                Type mappedOuterType = GetMappedType(sqlJoinStatement.Right.Sql);
                if (mappedOuterType == null)
                    return null;

                Type mappedInnerType = GetMappedType(sqlJoinStatement.Left.Sql);
                if (mappedInnerType == null)
                    return null;

                return CreateJoinExpression(sqlJoinStatement, mappedOuterType, mappedInnerType, "o", "i", sqlJoinStatement.OnClause, out elementType);

            case SqlTableRefExpression sqlTableRefStatement: 
                Type mappedType = GetMappedType(sqlTableRefStatement.Sql);
                if (mappedType == null)
                    return null;
                elementType = mappedType;
                IEnumerable<object> mappedCollection2 = GetMappedCollection(sqlTableRefStatement.Sql);
                return CreateRefExpression(sqlTableRefStatement, mappedType, mappedCollection2, "p");
        }
        return null;
    }

    public LambdaExpression? CreateExpression(SqlFromClause fromClause, out Type elementType) {
        elementType = null;
        var result = new List<Expression>();
        SqlTableExpression? expression = fromClause.TableExpressions.FirstOrDefault();
        if (expression == null) 
            return null;
        return CreateExpression(expression, out elementType);
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
    LambdaExpression CreateWhereExpression(SqlWhereClause whereClause, Type elementType) {
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

    void ProcessSelectStatement(SqlSelectStatement selectStatement)
    {
        var query = (SqlQuerySpecification)selectStatement.SelectSpecification.QueryExpression;
        var selectClause = query.SelectClause;
        //WriteLine($"Select columns {string.Join(", ", selectClause.SelectExpressions.Select(_ => _.Sql))}");
        //Type? mappedType = null;
        //IEnumerable<object> mappedCollection = _map[];
        SqlFromClause fromClause = query.FromClause;
        List<SqlPropertyRefColumn> schema = GetSchema(query.FromClause);
        LambdaExpression fromExpression = this.CreateExpression(query.FromClause, out Type fromExpressionReturnType);
        LambdaExpression whereExpression = this.CreateWhereExpression(query.WhereClause, fromExpressionReturnType);
        LambdaExpression selectExpression = this.CreateSelectExpression(query.SelectClause, fromExpressionReturnType, "sss", out Type? outputType);

        var xxx = fromExpression.Compile();
        var yyy = whereExpression.Compile();
        var sss = selectExpression.Compile();

        IEnumerable<object> zzz = (IEnumerable<object>)xxx.DynamicInvoke();
        IEnumerable<object> afterWhere = zzz.Where( yy => (bool)yyy.DynamicInvoke(yy) );
        //WriteLine(sss.ToString());
        WriteLine(fromExpression.ToString());
        WriteLine(whereExpression.ToString());
        WriteLine(selectExpression.ToString());
        var afterSelect = sss.DynamicInvoke( afterWhere.ToList() );
        WriteLine(JsonConvert.SerializeObject(afterSelect));

        //Type typeFuncTakesNothingReturnsIEnumerableOfDynamicType = typeof(Func<>).MakeGenericType(outputType);
        //LambdaExpression result = Expression.Lambda(typeFuncTakesNothingReturnsIEnumerableOfDynamicType, )
        //var zzz2 = yyy.Invoke();
       // foreach (var cxx in zzz) {

//        }

        Dictionary<Type, List<SqlPropertyRefColumn>>  columnsForWhereClausesGrouped = new Dictionary<Type, List<SqlPropertyRefColumn>>();
        foreach (SqlPropertyRefColumn sqlPropertyRefColumn in schema) {
            List<SqlPropertyRefColumn>? columnListForGroup = null;
            if (columnsForWhereClausesGrouped.ContainsKey(sqlPropertyRefColumn.MappedType)) 
            {
                columnListForGroup = columnsForWhereClausesGrouped[sqlPropertyRefColumn.MappedType];
            } else
            {
                columnListForGroup = new List<SqlPropertyRefColumn>();
                columnsForWhereClausesGrouped[sqlPropertyRefColumn.MappedType] = columnListForGroup;
            }
            columnListForGroup.Add(sqlPropertyRefColumn);
        }


/*
        dynamic resultType = new {  };
        var annotate = (SqlPropertyRefColumn s) => $"{s.SqlTableRefExpression.Sql}.{s.Property.Name}";
        foreach (SqlPropertyRefColumn sqlPropertyRefColumn in schema) {
            resultType[annotate(sqlPropertyRefColumn).Replace(".","_")] = GetDefault(sqlPropertyRefColumn.Property.PropertyType);
        }
        mappedType = resultType.GetType();
        */
        string columnString = string.Join(", \r\n\t\t", schema.Select( s => $"{s.SqlTableRefExpression.Sql}.{s.Property.Name}"));
        WriteLine($"columns \t\t{columnString}");
        /*
        Expression<Func<IEnumerable<dynamic>>> e = 
                        () => 
                            _customers
                                .Where(
                                    c => c.State == "MA")
                                .Select( 
                                    c => 
                                        new { c.Id, c.Name }); */
       // Expression<Func<IEnumerable<Customer>>> e = () => _customers;

        WriteLine($"from tables {string.Join(", ", fromClause.TableExpressions.Select(_ => _.Sql))}");
        
        MemberInfo? memberInfo = GetType().GetMember(nameof(_customers), BindingFlags.NonPublic | BindingFlags.Static).ToList().FirstOrDefault();
        MemberExpression memberAccess = Expression.MakeMemberAccess(null, memberInfo);

        //public static IEnumerable<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector);
        IEnumerable<MethodInfo> selectMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Select");

        //public static IEnumerable<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate);
        IEnumerable<MethodInfo> whereMethodInfos = 
            typeof(System.Linq.Enumerable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .ToList()
                .Where( mi => mi.Name == "Where");

        MethodInfo? selectMethodInfo = 
            selectMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );

        MethodInfo? whereMethodInfo = 
            whereMethodInfos
                .FirstOrDefault( 
                    mi => 
                        mi.IsGenericMethodDefinition 
                        && mi.GetParameters().Length == 2 
                        && mi.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>) );
               
        //Lambda whereLambda = Expression.Lambda();
        /*
        Type typeIEnumerableOfMappedType = typeof(IEnumerable<>).MakeGenericType( mappedType ); // == IEnumerable<mappedType>
        ParameterExpression paramOfTypeIEnumerableOfMappedType = Expression.Parameter(typeIEnumerableOfMappedType);

        ParameterExpression transformerParam = Expression.Parameter(mappedType, "t");
        Type funcTakingCustomerReturningCustomer = typeof(Func<,>).MakeGenericType(mappedType, mappedType);
        LambdaExpression transformer = Expression.Lambda(funcTakingCustomerReturningCustomer, transformerParam, transformerParam);
       
        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression selectMethodCall = Expression.Call(
            method: selectMethodInfo.MakeGenericMethod(new [] { mappedType, mappedType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, transformer}
        );

        ParameterExpression selectorParam = Expression.Parameter(mappedType, "c");
        Type funcTakingCustomerReturningBool = typeof(Func<,>).MakeGenericType(mappedType, typeof(bool));
        LambdaExpression selector = Expression.Lambda(funcTakingCustomerReturningBool, Expression.Constant(true), selectorParam);
        
        // Creating an expression for the method call and specifying its parameter.
        MethodCallExpression whereMethodCall = Expression.Call(
            method: whereMethodInfo.MakeGenericMethod(new [] { mappedType }),
            instance: null, 
            arguments: new Expression[] {paramOfTypeIEnumerableOfMappedType, selector}
        );

        Type funcTakingIEnumerableOfCustomerReturningIEnumerableOf = typeof(Func<,>).MakeGenericType(typeIEnumerableOfMappedType, typeIEnumerableOfMappedType);

        LambdaExpression l = Expression.Lambda(funcTakingIEnumerableOfCustomerReturningIEnumerableOf, whereMethodCall, new [] {paramOfTypeIEnumerableOfMappedType});
        
        Func<IEnumerable<Customer>, IEnumerable<Customer>> transform = (Func<IEnumerable<Customer>, IEnumerable<Customer>>)l.Compile();
        IEnumerable<Customer> transformed = transform(mappedCollection.OfType<Customer>().Cast<Customer>());
*/
        var whereClause = query.WhereClause;
        WriteLine($"where {whereClause.Expression.Sql}");
    }
}

// https://stackoverflow.com/questions/606104/how-to-create-linq-expression-tree-to-select-an-anonymous-type
public static class LinqRuntimeTypeBuilder
{
    //private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    private static System.Reflection.AssemblyName assemblyName = new System.Reflection.AssemblyName() { Name = "DynamicLinqTypes" };
    private static ModuleBuilder moduleBuilder = null;
    private static Dictionary<string, Type> builtTypes = new Dictionary<string, Type>();

    static LinqRuntimeTypeBuilder()
    {
        moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule(assemblyName.Name);
    }

    private static string GetTypeKey(Dictionary<string, Type> fields)
    {
        //TODO: optimize the type caching -- if fields are simply reordered, that doesn't mean that they're actually different types, so this needs to be smarter
        string key = string.Empty;
        foreach (var field in fields)
            key += field.Key + ";" + field.Value.Name + ";";

        return key;
    }

    public static Type GetDynamicType(Dictionary<string, Type> fields)
    {
        if (null == fields)
            throw new ArgumentNullException("fields");
        if (0 == fields.Count)
            throw new ArgumentOutOfRangeException("fields", "fields must have at least 1 field definition");

        try
        {
            Monitor.Enter(builtTypes);
            string className = GetTypeKey(fields);

            if (builtTypes.ContainsKey(className))
                return builtTypes[className];

            TypeBuilder typeBuilder = moduleBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);

            foreach (var field in fields)                    
                typeBuilder.DefineField(field.Key, field.Value, FieldAttributes.Public);

            builtTypes[className] = typeBuilder.CreateType();

            return builtTypes[className];
        }
        catch (Exception ex)
        {
            WriteLine( "ERROR: " + ex);
        }
        finally
        {
            Monitor.Exit(builtTypes);
        }

        return null;
    }


    private static string GetTypeKey(IEnumerable<PropertyInfo> fields)
    {
        return GetTypeKey(fields.ToDictionary(f => f.Name, f => f.PropertyType));
    }

    public static Type GetDynamicType(IEnumerable<PropertyInfo> fields)
    {
        return GetDynamicType(fields.ToDictionary(f => f.Name, f => f.PropertyType));
    }
}

//  https://stackoverflow.com/questions/15641339/create-new-propertyinfo-object-on-the-fly
public class MyObjectBuilder
{
    public static Type CompileResultType(string typeSignature, IEnumerable<Field> Fields)
    {
        TypeBuilder tb = GetTypeBuilder(typeSignature);
        ConstructorBuilder constructor = tb.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

        // NOTE: assuming your list contains Field objects with fields FieldName(string) and FieldType(Type)
        foreach (var field in Fields)
            CreateProperty(tb, field.FieldName, field.FieldType);

        Type objectType = tb.CreateType();
        return objectType;
    }

    private static TypeBuilder GetTypeBuilder(string typeSignature)
    {
        var an = new System.Reflection.AssemblyName(typeSignature);
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
        TypeBuilder tb = moduleBuilder.DefineType(typeSignature
                            , TypeAttributes.Public |
                            TypeAttributes.Class |
                            TypeAttributes.AutoClass |
                            TypeAttributes.AnsiClass |
                            TypeAttributes.BeforeFieldInit |
                            TypeAttributes.AutoLayout
                            , null);
        return tb;
    }

    private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType)
    {
        FieldBuilder fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);

        PropertyBuilder propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
        MethodBuilder getPropMthdBldr = tb.DefineMethod("get_" + propertyName, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, propertyType, Type.EmptyTypes);
        ILGenerator getIl = getPropMthdBldr.GetILGenerator();

        getIl.Emit(OpCodes.Ldarg_0);
        getIl.Emit(OpCodes.Ldfld, fieldBuilder);
        getIl.Emit(OpCodes.Ret);

        MethodBuilder setPropMthdBldr =
            tb.DefineMethod("set_" + propertyName,
              MethodAttributes.Public |
              MethodAttributes.SpecialName |
              MethodAttributes.HideBySig,
              null, new[] { propertyType });

        ILGenerator setIl = setPropMthdBldr.GetILGenerator();
        Label modifyProperty = setIl.DefineLabel();
        Label exitSet = setIl.DefineLabel();

        setIl.MarkLabel(modifyProperty);
        setIl.Emit(OpCodes.Ldarg_0);
        setIl.Emit(OpCodes.Ldarg_1);
        setIl.Emit(OpCodes.Stfld, fieldBuilder);

        setIl.Emit(OpCodes.Nop);
        setIl.MarkLabel(exitSet);
        setIl.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getPropMthdBldr);
        propertyBuilder.SetSetMethod(setPropMthdBldr);
    }
}

public class Field
{
   public string FieldName;
   public Type FieldType;
}

public class FieldMapping {
    public string OutputFieldName;
    public List<string> InputFieldName;
    public Type FieldType;
}