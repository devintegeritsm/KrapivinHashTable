// Program.cs in a new console project
using KrapivinHashTable;

class Program
{
    static void Main()
    {
        var hashTable = new KrapivinHashTable<string, object>();
        hashTable.Add("key1", "value1");
        hashTable.Add("key2", 42);

        Console.WriteLine(hashTable.Get("key1")); // value1
        Console.WriteLine(hashTable.Get("key2")); // 42
        Console.WriteLine(hashTable.Get("key3")); // null

        hashTable.Remove("key1");
        Console.WriteLine(hashTable.Get("key1")); // null
        Console.WriteLine(hashTable.Count);       // 1
    }
}