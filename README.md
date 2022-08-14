# csharp-sql2expression

https://docs.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.management.sqlparser.sqlcodedom?view=sql-smo-150
``` sql
        SELECT 
            dbo.Customers.Id, 
            dbo.Customers.Name, 
            dbo.Categories.Name
        FROM dbo.Customers 
        INNER JOIN dbo.Categories 
            ON dbo.Customers.CategoryId = dbo.Categories.Id
        WHERE dbo.Customers.State = 'MA'
```

from statement:
``` c#
() => _categories
        .Join(
            _customer, 
            outer => outer.Id, 
            inner => inner.CategoryId, 
            (outer, inner) => 
                new Dynamic_dbo_Customers_dbo_Categories() {
                    dbo_Customers = inner, 
                    dbo_Categories = outer
                })
```

where clause:
``` c#
    p => (p.dbo_Customers.State == "MA")
```

select clause:
``` c#
    (IEnumerable<object> Param_0) => 
        Param_0
            .Select(
                sss => 
                    new Dynamic_Dynamic_dbo_Customers_dbo_Categories() 
                    {
                        dbo_Customers_Id = Convert(sss, Dynamic_dbo_Customers_dbo_Categories).dbo_Customers.Id, 
                        dbo_Customers_Name = Convert(sss,Dynamic_dbo_Customers_dbo_Categories).dbo_Customers.Name,
                        dbo_Categories_Name = Convert(sss, Dynamic_dbo_Customers_dbo_Categories).dbo_Categories.Name
                    })
```
result:
``` json
    [   {
            "dbo_Customers_Id": 1,
            "dbo_Customers_Name": "Nic",
            "dbo_Categories_Name": "Tier 1"
        }
    ]
```