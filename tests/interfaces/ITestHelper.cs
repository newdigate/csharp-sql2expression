using Microsoft.SqlServer.Management.SqlParser.SqlCodeDom;
namespace tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public interface ITestHelper
{
    InvocationExpressionSyntax GetInvocationSyntax(IDictionary<SyntaxTree, CompilationUnitSyntax> trees);
    LocalDeclarationStatementSyntax GetLocalDeclarationStatement(IDictionary<SyntaxTree, CompilationUnitSyntax> trees);
    SqlSelectStatement? GetSingleSqlSelectStatement(string sql);
}
