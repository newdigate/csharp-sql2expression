using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class BasicSelectTest
{
    private readonly LambdaExpressionEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;
    private readonly LambdaStringToCSharpConverter _csharpConverter; 

    public BasicSelectTest() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEvaluator();
        SqlSelectStatementExpressionAdapterFactory factory =  new SqlSelectStatementExpressionAdapterFactory();
        _sqlSelectStatementExpressionAdapter = 
            factory
                .Create(dataSet.Map);
        _csharpConverter = factory.CreateLambdaExpressionConverter(dataSet.Map, dataSet.InstanceMap);
    }

    [Fact]
    public void TestSelectStatement()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE StateId = 1";
        const string expected = "_customers.Where(c => (c.StateId == 1)).Select(Param_0 => new {Id = Param_0.Id, Name = Param_0.Name})";

        ParseResult? parseResult = Parser.Parse(sql);
        SqlSelectStatement? selectStatement =
            parseResult.Script.Batches
                .SelectMany( b => b.Statements )
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;
        
        Xunit.Assert.NotNull(lambda);
        Xunit.Assert.Equal(expected, _csharpConverter.ConvertLambdaStringToCSharp(lambda.Body.ToString()));

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate<IEnumerable<object>>(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal("[{\"Id\":1,\"Name\":\"Nic\"}]", jsonResult);
    }
}
