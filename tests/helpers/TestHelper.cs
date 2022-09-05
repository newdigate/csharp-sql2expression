using Microsoft.SqlServer.Management.SqlParser.Parser;
using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
namespace tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class TestHelper : ITestHelper
{
    public SqlSelectStatement? GetSingleSqlSelectStatement(string sql)
    {
        var parseResult = Parser.Parse(sql);
        SqlSelectStatement? selectStatement =
            parseResult.Script.Batches
                .SelectMany(b => b.Statements)
                .OfType<SqlSelectStatement>()
                .Cast<SqlSelectStatement>()
                .FirstOrDefault();
        return selectStatement;
    }


    public InvocationExpressionSyntax GetInvocationSyntax(IDictionary<SyntaxTree, CompilationUnitSyntax> trees)
    {
        LocalDeclarationStatementSyntax varx = GetLocalDeclarationStatement(trees);

        InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)varx.Declaration.Variables.First().Initializer.Value;
        return invocation;
    }

    public LocalDeclarationStatementSyntax GetLocalDeclarationStatement(IDictionary<SyntaxTree, CompilationUnitSyntax> trees)
    {
        MethodDeclarationSyntax method =
            trees
                .Values
                .First()
                .SyntaxTree
                .GetCompilationUnitRoot()
                .DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .First();

        LocalDeclarationStatementSyntax varx = method.Body.Statements.OfType<LocalDeclarationStatementSyntax>().First();
        return varx;
    }


}
