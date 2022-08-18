namespace src;

public class UniqueNameProvider : IUniqueNameProvider {
    private readonly List<string> _existingNames = new List<string>();

    public string GetUniqueName(string inputName) {
        string current = inputName;
        int counter = 2;
        while (_existingNames.Contains(current)) {
            current = inputName + (counter++).ToString();
        }
        _existingNames.Add(current);
        return current;
    }
}