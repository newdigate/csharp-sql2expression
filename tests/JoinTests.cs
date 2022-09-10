using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using Newtonsoft.Json;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using src;

namespace tests;

public class JoinTests
{
    private readonly LambdaExpressionEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;
    private readonly LambdaStringToCSharpConverter _csharpConverter; 
    private readonly ICSharpCompilationProvider _csharpCompilationProvider;
    private readonly ITestHelper _testHelper;
    private readonly IAssertHelper _assertHelper;
    private readonly IInvocationExpressionSyntaxHelper _invocationExpressionSyntaxHelper;

    public JoinTests() {
        TestDataSet dataSet = new TestDataSet();
        _testHelper = new TestHelper();
        _assertHelper = new AssertHelper();
        _lambdaEvaluator = new LambdaExpressionEvaluator();
        SqlSelectStatementExpressionAdapterFactory factory =  new SqlSelectStatementExpressionAdapterFactory();
        _sqlSelectStatementExpressionAdapter = factory.Create(dataSet.Map);
        _csharpConverter = factory.CreateLambdaExpressionConverter(dataSet.Map, dataSet.InstanceMap);
        _csharpCompilationProvider = new CSharpCompilationProvider(dataSet.DefaultReferences);
        _invocationExpressionSyntaxHelper = new InvocationExpressionSyntaxHelper(_csharpCompilationProvider, _testHelper, _csharpConverter);
    }

    [Fact]
    public void TestSelectJoinStatement()
    {
        const string sql = 
@"SELECT dbo.Customers.Id, dbo.Customers.Name, dbo.Categories.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.StateId = 1";
        SqlSelectStatement? selectStatement = _testHelper.GetSingleSqlSelectStatement(sql);
        LambdaExpression? lambda = selectStatement != null ?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;
        Assert.NotNull(lambda);

        List<InvocationExpressionSyntax> chainedInvocations = 
            _invocationExpressionSyntaxHelper
                .GetChainedInvokations(lambda.Body.ToString());

        InvocationExpressionSyntax selectInvocation = chainedInvocations[0];
        Assert.Equal(
            "Select",  
            ((MemberAccessExpressionSyntax)selectInvocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertInitializers(
                selectInvocation,
                "dbo_Customers_Id = Param_0.dbo_Customers.Id",
                "dbo_Customers_Name = Param_0.dbo_Customers.Name",
                "dbo_Categories_Name = Param_0.dbo_Categories.Name");

        InvocationExpressionSyntax whereInvocation = chainedInvocations[1];
        Assert.Equal(
            "Where", 
            ((MemberAccessExpressionSyntax)whereInvocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertArguments(
                whereInvocation.ArgumentList.Arguments,
                "c => (c.dbo_Customers.StateId == 1)");

        InvocationExpressionSyntax joinInvocation = chainedInvocations[2];
        Assert.Equal(
            "Join", 
            (joinInvocation.Expression as MemberAccessExpressionSyntax)?.Name.ToFullString());
        _assertHelper
            .AssertArguments(
                joinInvocation.ArgumentList.Arguments,
                "_categories", 
                "outer => outer.CategoryId", 
                "inner => inner.Id", 
                "(outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}");

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);
        string jsonResult = JsonConvert.SerializeObject(result);
        System.Diagnostics.Debug.WriteLine(jsonResult);

        Assert.Equal(jsonResult, "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\"}]");
    }

    [Fact]
    public void TestSelectDoubleJoinStatement()
    {
        const string sql = @"
SELECT dbo.Customers.Id, dbo.Customers.Name,dbo.Categories.Name, dbo.States.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
WHERE dbo.States.Name = 'MA'";
        SqlSelectStatement? selectStatement = _testHelper.GetSingleSqlSelectStatement(sql);
        LambdaExpression? lambda = selectStatement != null ?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;
        Assert.NotNull(lambda);

        List<InvocationExpressionSyntax> chainedInvocations = 
            _invocationExpressionSyntaxHelper
                .GetChainedInvokations(lambda.Body.ToString());

        InvocationExpressionSyntax selectInvocation = chainedInvocations[0];
        Assert.Equal(
            "Select",  
            ((MemberAccessExpressionSyntax)selectInvocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertInitializers(
                selectInvocation,
                "dbo_Customers_Id = Param_0.dbo_Customers.Id", 
                "dbo_Customers_Name = Param_0.dbo_Customers.Name", 
                "dbo_Categories_Name = Param_0.dbo_Categories.Name", 
                "dbo_States_Name = Param_0.dbo_States.Name"
               );

        InvocationExpressionSyntax whereInvocation = chainedInvocations[1];
        Assert.Equal(
            "Where", 
            ((MemberAccessExpressionSyntax)whereInvocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertArguments(
                whereInvocation.ArgumentList.Arguments,
                "c => (c.dbo_States.Name == \"MA\")");
        
        InvocationExpressionSyntax join1Invocation = chainedInvocations[2];
        Assert.Equal(
            "Join", 
            ((MemberAccessExpressionSyntax)join1Invocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertArguments(
                join1Invocation.ArgumentList.Arguments,
                "_states",
                "outer => outer.dbo_Customers.StateId",
                "inner => inner.Id",
                "(outer, inner) => new {dbo_States = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories}");
        LambdaExpressionSyntax? fourthParameter = join1Invocation.ArgumentList.Arguments[3].Expression as LambdaExpressionSyntax;
        AnonymousObjectCreationExpressionSyntax? anonymousObjectCreationExpressionSyntax = fourthParameter?.Body as AnonymousObjectCreationExpressionSyntax;
        _assertHelper
            .AssertInitializers(
                anonymousObjectCreationExpressionSyntax, 
                "dbo_States = inner", 
                "dbo_Customers = outer.dbo_Customers", 
                "dbo_Categories = outer.dbo_Categories");

        InvocationExpressionSyntax join2Invocation = chainedInvocations[3];
        Assert.Equal(
            "Join", 
            ((MemberAccessExpressionSyntax)join2Invocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertArguments(
                join2Invocation.ArgumentList.Arguments,
                "_categories",
                "outer => outer.CategoryId",
                "inner => inner.Id",
                "(outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}");

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        _assertHelper
            .AssertDynamicProperties(
                result.First(), 
                new Dictionary<string,object>{ 
                    {"dbo_Customers_Id", 1},
                    {"Id2", 1},
                    {"Id3", 1},
                    {"Id4", 1},
                    {"dbo_Categories_Name", "Tier 1"},
                    {"dbo_Customers_Name", "Nic"},
                    {"dbo_States_Name", "MA"},
                    {"dbo_Brands_Name","Coke"},
                    {"CategoryId", 1},
                    {"BrandId", 1},
                    {"StateId", 1}
                });

        string jsonResult = JsonConvert.SerializeObject(result);
        System.Diagnostics.Debug.WriteLine(jsonResult);  

        Assert.Equal(
            "[{\"dbo_Customers_Id\":1,\"dbo_Customers_Name\":\"Nic\",\"dbo_Categories_Name\":\"Tier 1\",\"dbo_States_Name\":\"MA\"}]",
            jsonResult);
    }

    [Fact]
    public void TestSelectTripleJoinStatement()
    {
        const string sql = @"
SELECT dbo.Customers.Id, dbo.Customers.Name,dbo.Categories.Name, dbo.States.Name, dbo.Brands.Name
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
INNER JOIN dbo.Brands ON dbo.Customers.BrandId = dbo.Brands.Id
WHERE dbo.States.Name = 'MA' and dbo.Brands.Name = 'Coke' ";
        SqlSelectStatement? selectStatement = _testHelper.GetSingleSqlSelectStatement(sql);
        LambdaExpression? lambda = selectStatement != null ?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;
        Assert.NotNull(lambda);

        List<InvocationExpressionSyntax> chainedInvocations = 
            _invocationExpressionSyntaxHelper
                .GetChainedInvokations(lambda.Body.ToString());
        
        InvocationExpressionSyntax selectInvocation = chainedInvocations[0];
        Assert.Equal(
            "Select",  
            ((MemberAccessExpressionSyntax)selectInvocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertInitializers(
                selectInvocation,
                "dbo_Customers_Id = Param_0.dbo_Customers.Id", 
                "dbo_Customers_Name = Param_0.dbo_Customers.Name", 
                "dbo_Categories_Name = Param_0.dbo_Categories.Name", 
                "dbo_States_Name = Param_0.dbo_States.Name", 
                "dbo_Brands_Name = Param_0.dbo_Brands.Name");

        InvocationExpressionSyntax whereInvocation = chainedInvocations[1];
        Assert.Equal(
            "Where", 
            ((MemberAccessExpressionSyntax)whereInvocation.Expression).Name.ToFullString());
        _assertHelper
            .AssertArguments(
                whereInvocation.ArgumentList.Arguments,
                "c => ((c.dbo_States.Name == \"MA\") && (c.dbo_Brands.Name == \"Coke\"))");

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);
        _assertHelper
            .AssertDynamicProperties(
                result.First(), 
                new Dictionary<string,object>{ 
                    {"dbo_Customers_Id", 1},
                    {"dbo_Categories_Name", "Tier 1"},
                    {"dbo_Customers_Name", "Nic"},
                    {"dbo_States_Name", "MA"},
                    {"dbo_Brands_Name","Coke"},
                });
    }

    [Fact]
    public void TestSelectStarFromTripleJoinStatement()
    {
        const string sql = @"
SELECT *
FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
INNER JOIN dbo.Brands ON dbo.Customers.BrandId = dbo.Brands.Id
WHERE dbo.States.Name = 'MA'";
        const string expected = "_customers.Join(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).Join(_states, outer => outer.dbo_Customers.StateId, inner => inner.Id, (outer, inner) => new {dbo_States = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories}).Join(_brands, outer => outer.dbo_Customers.BrandId, inner => inner.Id, (outer, inner) => new {dbo_Brands = inner, dbo_Customers = outer.dbo_Customers, dbo_Categories = outer.dbo_Categories, dbo_States = outer.dbo_States}).Where(c => (c.dbo_States.Name == \"MA\")).Select(Param_0 => new {Id = Param_0.dbo_Categories.Id, Name = Param_0.dbo_Categories.Name, CategoryId = Param_0.dbo_Customers.CategoryId, StateId = Param_0.dbo_Customers.StateId, BrandId = Param_0.dbo_Customers.BrandId, Id2 = Param_0.dbo_Customers.Id, Name2 = Param_0.dbo_Customers.Name, Id3 = Param_0.dbo_States.Id, Name3 = Param_0.dbo_States.Name, Id4 = Param_0.dbo_Brands.Id, Name4 = Param_0.dbo_Brands.Name})";
        SqlSelectStatement? selectStatement = _testHelper.GetSingleSqlSelectStatement(sql);

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Assert.NotNull(lambda);

        List<InvocationExpressionSyntax> chainedInvocations = 
            _invocationExpressionSyntaxHelper
                .GetChainedInvokations(lambda.Body.ToString());

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);
        _assertHelper
            .AssertDynamicProperties(
                result.First(), 
                new Dictionary<string,object>{ 
                    {"Id", 1},
                    {"Id2", 1},
                    {"Id3", 1},
                    {"Id4", 1},
                    {"Name", "Tier 1"},
                    {"Name2", "Nic"},
                    {"Name3", "MA"},
                    {"Name4", "Coke"},
                    {"CategoryId", 1},
                    {"BrandId", 1},
                    {"StateId", 1}
                });
    }

    [Fact]
    public void TestSelectStarFromLeftOuterTripleJoinStatement()
    {
        const string sql = @"
SELECT *
FROM dbo.Customers 
LEFT OUTER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
WHERE dbo.Customers.Name = 'Nic'";
        const string expected = "_customers.GroupJoin(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).SelectMany(x => x.dbo_Categories.DefaultIfEmpty(), (x, ii) => new {dbo_Customers = x.dbo_Customers, dbo_Categories = ii}).Where(c => (c.dbo_Customers.Name == \"Nic\")).Select(Param_0 => new {Id = Param_0.dbo_Categories.Id, Name = Param_0.dbo_Categories.Name, CategoryId = Param_0.dbo_Customers.CategoryId, Name2 = Param_0.dbo_Customers.Name, Id2 = Param_0.dbo_Customers.Id, StateId = Param_0.dbo_Customers.StateId, BrandId = Param_0.dbo_Customers.BrandId})";
        
        SqlSelectStatement? selectStatement = _testHelper.GetSingleSqlSelectStatement(sql);

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Assert.NotNull(lambda);
        Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);        
        Assert.Equal(1, result.Count());
        _assertHelper
            .AssertDynamicProperties(
                result.First(), 
                new Dictionary<string,object>{ 
                    {"Id", 1},
                    {"Id2", 1},
                    {"Name", "Tier 1"},
                    {"Name2", "Nic"},
                    {"CategoryId", 1},
                    {"BrandId", 1},
                    {"StateId", 1}
                });
    }

    [Fact]
    public void TestSelectStarFromDoubleLeftOuterJoinStatement()
    {
        const string sql = @"
SELECT *
FROM dbo.Customers 
LEFT OUTER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
LEFT OUTER JOIN dbo.Brands ON dbo.Customers.BrandId = dbo.Brands.Id
WHERE dbo.Customers.Name = 'Nic'";
        const string expected = "_customers.GroupJoin(_categories, outer => outer.CategoryId, inner => inner.Id, (outer, inner) => new {dbo_Categories = inner, dbo_Customers = outer}).SelectMany(x => x.dbo_Categories.DefaultIfEmpty(), (oo, ii) => new {dbo_Customers = oo.dbo_Customers, dbo_Categories = ii}).Where(c => (c.dbo_Customers.Name == \"Nic\")).Select(Param_0 => new {Id = Param_0.dbo_Customers.Id, Name = Param_0.dbo_Customers.Name, StateId = Param_0.dbo_Customers.StateId, CategoryId = Param_0.dbo_Customers.CategoryId, BrandId = Param_0.dbo_Customers.BrandId, Id2 = Param_0.dbo_Categories.Id, Name2 = Param_0.dbo_Categories.Name})";
        
        SqlSelectStatement? selectStatement = _testHelper.GetSingleSqlSelectStatement(sql);

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;
        Assert.NotNull(lambda);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda);        
        Assert.Equal(1, result.Count());
        _assertHelper
            .AssertDynamicProperties(
                result.First(), 
                new Dictionary<string,object>{ 
                    {"Id", 1},
                    {"Id2", 1},
                    {"Id3", 1},
                    {"Name", "Tier 1"},
                    {"Name2", "Nic"},
                    {"Name3", "Coke"},
                    {"CategoryId", 1},
                    {"BrandId", 1},
                    {"StateId", 1}
                });

    }

}
