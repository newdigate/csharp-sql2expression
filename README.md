# sql2expression

A quick and dirty proof-of-concept to convert a SQL select statement into a LINQ expression. 

I'm using [microsoft.sqlserver.management.sqlparser.sqlcodedom](https://docs.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.management.sqlparser.sqlcodedom?view=sql-smo-150) to parse sql scripts.

consider the sql select statement
``` sql
SELECT Id, Name FROM dbo.Customers WHERE StateId = 1
```

if we map ```dbo.Customer``` to an instance of ```IEnumerable<Customer>```
``` c#
    private readonly Customer[] _customers = 
        new [] { new Customer() {Id = 1, Name="Nic", StateId=1}};

    private readonly Dictionary<string, IEnumerable<object>> _map = 
        new Dictionary<string, IEnumerable<object>>{
            { "dbo.Customers", _customers}};
```

where ```Customer``` is
``` c#
public class Customer {
    public int Id { get; set; }
    public string Name { get; set; }
    public int StateId { get; set; }
}
```
then we can translate the ```from```, ```where``` and ```select``` sql clause to c# expressions:

```from```:
``` c#
    () => _customers
```

```where```:
``` c#
    c => (c.StateId == 1)
```

```select```:
``` c#
    (IEnumerable<dynamic> collection) => 
        collection
            .Select(customer => new {Id = customer.Id, Name = customer.Name})
```
then we combine these expressions into a single lambda expression taking no arguments and returning an ```IEnumerable<dynamic>```

``` c#
() => 
    Invoke( 
        collection => 
            collection
                .Select(
                    p => new {
                        Id = p.Id, 
                        Name = p.Name}), 
        _customers
            .Where(
                p => (p.StateId == 1)))
```
when we evaluate the expression, the result (serialized to json) is:
``` javascript
[{"Id":1,"Name":"Nic"}]
```

# party trick
from [TestSelectTripleJoinStatement](tests/UnitTest1.cs#L84)

given the query:
``` sql
SELECT * FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
INNER JOIN dbo.Brands ON dbo.Customers.BrandId = dbo.Brands.Id
WHERE dbo.States.Name = 'MA'";
```

```from:```
``` c#
() => 
    Invoke(
        collection => collection
            .Select(p => 
                new () {
                    CategoryId = p.dbo_Customers.CategoryId,
                    StateId = p.dbo_Customers.StateId, 
                    BrandId = p.dbo_Customers.BrandId, 
                    Id = p.dbo_Customers.Id, 
                    Name = p.dbo_Customers.Name, 
                    Id2 = p.dbo_Categories.Id, 
                    Name2 = p.dbo_Categories.Name, 
                    Id3 = p.dbo_States.Id, 
                    Name3 = p.dbo_States.Name, 
                    Id4 = p.dbo_Brands.Id, 
                    Name4 = p.dbo_Brands.Name
                }), 
        _brands
            .Join(
                _states
                    .Join(
                        _categories
                            .Join(
                                _customers, 
                                right => right.Id, 
                                left => left.CategoryId, 
                                (right, left) => new {
                                    dbo_Customers = left, 
                                    dbo_Categories = right}), 
                        right => right.Id, 
                        left => left.dbo_Customers.StateId, 
                        (right, left) => new {
                            dbo_Customers = left.dbo_Customers,
                            dbo_Categories = left.dbo_Categories,
                            dbo_States = right}), 
                right => right.Id, 
                left => left.dbo_Customers.BrandId, 
                (right, left) => new {
                    dbo_Customers = left.dbo_Customers, 
                    dbo_Categories = left.dbo_Categories, 
                    dbo_States = left.dbo_States, 
                    dbo_Brands = right})
            .Where(p => (p.dbo_States.Name == "MA")))
```
```results:```
``` javascript
[{"CategoryId":1,"StateId":1,"BrandId":1,"Id":1,"Name":"Nic","Id2":1,"Name2":"Tier 1","Id3":1,"Name3":"MA","Id4":1,"Name4":"Coke"}]
```
