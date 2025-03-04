using KrapivinHashTable;

class Program
{
    static void Main()
    {
        var hashTable = new KrapivinHashTable<string, object>();
        hashTable.Insert("key1", "value1");
        Console.WriteLine(hashTable.Get("key1")); // Should print: value1
    }
}