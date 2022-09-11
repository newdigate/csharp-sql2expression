namespace src;

public class FieldMapping {
    public string OutputFieldName;
    public List<string> InputFieldName;
    public Type FieldType;
    public bool IsNullable { get; set; }
}
