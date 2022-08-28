using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class BasicSelectTest
{
    private readonly LambdaExpressionEnumerableEvaluator _lambdaEvaluator;

    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;

    public BasicSelectTest() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEnumerableEvaluator();
        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapterFactory()
                .Create(dataSet.Map);
    }

    [Fact]
    public void TestSelectStatement()
    {
        const string sql = "SELECT Id, Name FROM dbo.Customers WHERE StateId = 1";
        var parseResult = Parser.Parse(sql);

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
        string expressionString = lambda.ToString();
        Xunit.Assert.Equal(
            "() => value(tests.Customer[]).Where(c => (c.StateId == 1)).Select(Param_0 => new Dynamic_Customer() {Id = Param_0.Id, Name = Param_0.Name})",
            expressionString);

        IEnumerable<object>? result = _lambdaEvaluator.Evaluate(lambda); 
        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal("[{\"Id\":1,\"Name\":\"Nic\"}]", jsonResult);
    }
}
