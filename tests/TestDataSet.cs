using Microsoft.CodeAnalysis;

namespace tests;

public class TestDataSet {
    public static readonly Customer[] _customers = new [] { new Customer() {Id = 1, Name="Nic", StateId=1, CategoryId=1, BrandId=1}};
    public static readonly Category[] _categories = new [] { new Category() {Id = 1, Name="Tier 1"}};
    public static readonly State[] _states = new [] { new State() { Id = 1, Name = "MA" }};
    public static readonly Brand[] _brands = new [] { new Brand() { Id = 1, Name = "Coke" }};
    public readonly Dictionary<string, IEnumerable<object>> Map = 
        new Dictionary<string, IEnumerable<object>>{
            { "dbo.Customers", _customers},
            { "dbo.States", _states},
            { "dbo.Brands", _brands},
            { "dbo.Categories", _categories}};
            
    public readonly Dictionary<Type, string> InstanceMap = 
        new Dictionary<Type, string>{
            {   typeof(Customer),   nameof(_customers)},
            {   typeof(State),      nameof(_states)},
            {   typeof(Brand),      nameof(_brands)},
            {   typeof(Category),   nameof(_categories)}
        };  

    public readonly MetadataReference[] DefaultReferences = 
            new[] { 
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(SyntaxTree).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(NuGet.Frameworks.CompatibilityTable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(tests.TestDataSet).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location ),
                MetadataReference.CreateFromFile(Path.Combine(Directory.GetParent(typeof(object)?.Assembly.Location).FullName, "System.Runtime.dll")),
            };  
}
