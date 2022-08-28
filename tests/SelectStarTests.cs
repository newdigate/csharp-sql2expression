using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;
using Newtonsoft.Json;

namespace tests;
using src;

public class SelectStarTests
{
    private readonly LambdaExpressionEnumerableEvaluator _lambdaEvaluator;
    private readonly SqlSelectStatementExpressionAdapter _sqlSelectStatementExpressionAdapter;

    public SelectStarTests() {
        TestDataSet dataSet = new TestDataSet();
        _lambdaEvaluator = new LambdaExpressionEnumerableEvaluator();
        _sqlSelectStatementExpressionAdapter = 
            new SqlSelectStatementExpressionAdapterFactory()
                .Create(dataSet.Map);
    }

    [Fact]
    public void TestSelectStarStatement()
    {
        const string sql = "SELECT * FROM dbo.Customers WHERE StateId = 1";
        var parseResult = Parser.Parse(sql);

        SqlSelectStatement? selectStatement =
            parseResult.Script.Batches
                .SelectMany( b => b.Statements)
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();

        LambdaExpression? lambda = selectStatement != null?
            _sqlSelectStatementExpressionAdapter
                .ProcessSelectStatement(selectStatement) : null;

        Xunit.Assert.NotNull(lambda);
        string expressionString = lambda.ToString();
        Xunit.Assert.Equal(
            "() => value(tests.Customer[]).Where(c => (c.StateId == 1)).Select(Param_0 => new Dynamic_Customer() {StateId = Param_0.StateId, Id = Param_0.Id, Name = Param_0.Name, CategoryId = Param_0.CategoryId, BrandId = Param_0.BrandId})",
            expressionString);

        IEnumerable<object> result = _lambdaEvaluator.Evaluate(lambda); 

        string jsonResult = JsonConvert.SerializeObject(result);
        WriteLine(jsonResult);  

        Xunit.Assert.Equal(jsonResult, "[{\"StateId\":1,\"Id\":1,\"Name\":\"Nic\",\"CategoryId\":1,\"BrandId\":1}]");
    }
}
