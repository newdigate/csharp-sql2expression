namespace tests;

using System.Text.RegularExpressions;
using src;

public class LambdaStringToCSharpConverter {

    private readonly ITypeMapper _typeMapper;
    private readonly IInstanceMapper _instanceMapper;

    public LambdaStringToCSharpConverter(ITypeMapper typeMapper, IInstanceMapper instanceMapper)
    {
        _typeMapper = typeMapper;
        _instanceMapper = instanceMapper;
    }

    public string ConvertLambdaStringToCSharp(string lambdaString) {
        
        foreach (string typeKey in _typeMapper.GetKeys()) {
            Type? t = _typeMapper.GetMappedType(typeKey);
            if (t == null) continue;
            string? instanceName = _instanceMapper.GetInstanceName(t);
            if (String.IsNullOrEmpty(instanceName))
                continue;       
            lambdaString = lambdaString.Replace($"value({t.FullName}[])", instanceName);
        }

        Regex newExpressions = new Regex("new [A-Za-z_][A-Za-z0-9_]*\\(\\)");
        lambdaString = newExpressions.Replace(lambdaString, "new");

        return 
            lambdaString
                .Replace(" AndAlso ", " && ")
                .Replace(" And ", " & ")
                .Replace(" OrElse ", " || ")
                .Replace(" Or ", " | ");
    }
}