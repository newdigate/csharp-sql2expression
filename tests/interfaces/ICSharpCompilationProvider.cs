using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace tests;

public interface ICSharpCompilationProvider {
    CSharpCompilation CompileCSharp(
        IEnumerable<string> sourceCodes, 
        out IDictionary<SyntaxTree, CompilationUnitSyntax> roots);
}
