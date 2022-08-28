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
}
