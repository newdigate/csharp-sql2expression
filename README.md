# sql2expression

A proof-of-concept to convert a SQL select statement into a LINQ expression, using [microsoft.sqlserver.management.sqlparser.sqlcodedom](https://docs.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.management.sqlparser.sqlcodedom?view=sql-smo-150) to parse sql scripts.

consider the sql select statement:
``` sql
SELECT Id, Name FROM dbo.Customers WHERE StateId = 1
```

if we map ```dbo.Customer``` to an instance of ```IEnumerable<Customer>```:
``` c#
    private readonly Customer[] _customers = 
        new [] { new Customer() {Id = 1, Name="Nic", StateId=1}};

    private readonly Dictionary<string, IEnumerable<object>> _map = 
        new Dictionary<string, IEnumerable<object>>{
            { "dbo.Customers", _customers}};
```

where ```Customer``` is:
``` c#
public class Customer {
    public int Id { get; set; }
    public string Name { get; set; }
    public int StateId { get; set; }
}
```
then we can translate the ```from```, ```where``` and ```select``` sql clauses to c# expressions:

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
    (Customer customer) => new {Id = customer.Id, Name = customer.Name})
```
combining these expressions into a single lambda expression taking no arguments and returning an ```IEnumerable<dynamic>```:

``` c#
() => 
    _customers
        .Where(
            p => (p.StateId == 1))
        .Select(
            p => new {
                Id = p.Id, 
                Name = p.Name})

```
when we evaluate the expression, the result (serialized to json) is:
``` javascript
[{"Id":1,"Name":"Nic"}]
```

# party trick
from [TestSelectTripleJoinStatement](tests/UnitTest1.cs#L84)

when given the query:
``` sql
SELECT * FROM dbo.Customers 
INNER JOIN dbo.Categories ON dbo.Customers.CategoryId = dbo.Categories.Id
INNER JOIN dbo.States ON dbo.Customers.StateId = dbo.States.Id
INNER JOIN dbo.Brands ON dbo.Customers.BrandId = dbo.Brands.Id
WHERE dbo.States.Name = 'MA'";
```

we should get this expression:
``` c#
() => _brands
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
        .Select(p => new {
            dbo_Customers_Id = p.dbo_Customers.Id, 
            dbo_Customers_Name = p.dbo_Customers.Name, 
            dbo_Categories_Name = p.dbo_Categories.Name, 
            dbo_States_Name = p.dbo_States.Name, 
            dbo_Brands_Name = p.dbo_Brands.Name})
```


and when evaluating this expression for the data in the unit test, we should get these results:

``` javascript
[{"dbo_Customers_Id":1,"dbo_Customers_Name":"Nic","dbo_Categories_Name":"Tier 1","dbo_States_Name":"MA","dbo_Brands_Name":"Coke"}]
```
