# KrapivinHashTable

A high-performance, segmented hash table implementation for .NET with quadratic probing and efficient collision resolution.

## Features

- Generic implementation supporting any non-null key and value types
- Segmented design for improved cache locality
- Efficient quadratic probing for collision resolution
- MurmurHash3 for high-quality hash distribution
- Logical deletion to maintain probe sequences
- Customizable capacity and segment sizes
- Support for custom equality comparers

## Installation

### NuGet Package Manager

```
Install-Package KrapivinHashTable
```

### .NET CLI

```
dotnet add package KrapivinHashTable
```

## Quick Start

```csharp
// Create a new hash table with default settings
var hashTable = new KrapivinHashTable<string, int>();

// Insert key-value pairs
hashTable.Insert("one", 1);
hashTable.Insert("two", 2);
hashTable.Insert("three", 3);

// Retrieve values
int value;
if (hashTable.TryGet("two", out value))
{
    Console.WriteLine($"Found: {value}");  // Outputs: Found: 2
}

// Alternative retrieval method
int anotherValue = hashTable.Get("three");  // Returns 3
int notFound = hashTable.Get("four");       // Returns default(int) which is 0

// Delete a key-value pair
bool deleted = hashTable.Delete("one");     // Returns true
```

## Configuration Options

The `KrapivinHashTable` constructor accepts several parameters to customize the table's behavior:

```csharp
public KrapivinHashTable(
    int initialCapacity = 1024,             // Total number of slots
    int segmentSize = 32,                   // Size of each segment
    IEqualityComparer<TKey>? comparer = null // Custom equality comparer
)
```

- **initialCapacity**: The total number of slots in the hash table. Default is 1024.
- **segmentSize**: The size of each segment. This affects the search locality. Default is 32.
- **comparer**: A custom equality comparer for the key type. If null, the default comparer for the key type is used.

## Technical Details

### Segmented Design

KrapivinHashTable uses a segmented approach where the hash table is divided into segments of fixed size. When searching for a key, the hash function first identifies the appropriate segment, and then the search is initially confined to that segment. This improves cache locality and performance.

### Collision Resolution

Collisions are resolved using quadratic probing with the formula `(n² + n)/2`, which provides a good distribution and helps avoid clustering. When a segment becomes full, the search extends beyond the segment using the same quadratic probing sequence.

### Hash Function

The library uses MurmurHash3, a high-quality non-cryptographic hash function that provides excellent distribution with minimal collisions.

### Hash distribution for non-string types

- .NET's GetHashCode() implementations are already designed to provide good distribution for hash tables
- For most practical use cases, this distribution is sufficient
- The additional MurmurHash3 pass might provide marginally better distribution in some edge cases, but at a significant performance cost
- When dealing with large datasets, the performance gain from avoiding allocations likely outweighs any theoretical distribution improvements

### Performance Considerations

- The table automatically prevents insertions when the load factor exceeds 90% to maintain good performance.
- Logical deletion is used to maintain probe sequences, which means deleted slots can be reused for new insertions.
- The library is optimized for lookup operations, making it suitable for caching scenarios.

## Use Cases

- In-memory caching
- Fast lookup tables
- Symbol tables in compilers and interpreters
- Counting unique elements
- Implementing sets and dictionaries

## License

MIT License

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request
