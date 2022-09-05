namespace tests;

public class Brand {
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Category {
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Customer {
    public int Id { get; set; }
    public string Name { get; set; }
    public int StateId { get; set; }
    public int CategoryId { get; set; }
    public int BrandId {get; set;}
}

public class State {
    public int Id { get; set; }
    public string Name { get; set; }
}
