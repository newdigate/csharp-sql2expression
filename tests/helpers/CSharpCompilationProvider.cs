using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace tests;

public class CSharpCompilationProvider : ICSharpCompilationProvider {
    private readonly IEnumerable<MetadataReference> references;
    public CSharpCompilationProvider(IEnumerable<MetadataReference> references)
    {
        this.references = references;
    }
    public CSharpCompilation CompileCSharp(
        IEnumerable<string> sourceCodes, 
        out IDictionary<SyntaxTree, CompilationUnitSyntax> roots) 
    {
        Dictionary<SyntaxTree, CompilationUnitSyntax> syntaxTrees = new Dictionary<SyntaxTree, CompilationUnitSyntax>();
        roots = syntaxTrees;

        foreach (string sourceCode in sourceCodes) {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            syntaxTrees[tree] = root;
        }
        CSharpCompilationOptions cSharpCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        CSharpCompilation compilation = 
            CSharpCompilation
                .Create(
                    "assemblyName",
                    syntaxTrees.Keys,
                    references,
                    cSharpCompilationOptions
                   );
        foreach (var d in compilation.GetDiagnostics())
        {
            Console.WriteLine(CSharpDiagnosticFormatter.Instance.Format(d));
        }
        return compilation;
    }

}
