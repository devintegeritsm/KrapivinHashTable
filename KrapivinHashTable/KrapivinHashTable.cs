using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MurmurHash.Net;

namespace KrapivinHashTable
{
    public class KrapivinHashTable<TKey, TValue> where TKey : notnull
    {
        private readonly int capacity;
        private readonly int segmentSize;
        private readonly Entry[] table;
        private readonly IEqualityComparer<TKey> comparer;
        private int count;

        private class Entry
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
            public bool IsOccupied { get; set; }

            public Entry(TKey key, TValue value)
            {
                Key = key;
                Value = value;
                IsOccupied = true;
            }
        }

        public KrapivinHashTable(int initialCapacity = 1024, int segmentSize = 32, IEqualityComparer<TKey> comparer = null)
        {
            if (initialCapacity < segmentSize || segmentSize <= 0)
                throw new ArgumentException("Invalid capacity or segment size.");

            this.capacity = initialCapacity;
            this.segmentSize = segmentSize;
            this.table = new Entry[capacity];
            this.count = 0;
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        // Hash function using MurmurHash.Net

        private static uint Hash(TKey key)
        {
            const uint seed = 0; // Default seed value

            if (key is string str)
            {
                // For strings, use a Memory-based approach to avoid unnecessary allocations
                ReadOnlyMemory<char> memory = str.AsMemory();

                // UTF-8 encoding inplementation
                // byte[] utf8Bytes = System.Text.Encoding.UTF8.GetBytes(str);

                // NOTE: This is different from the original implementation which used UTF-8 encoding.
                // - PRO: Zero allocations for string hashing
                // - PRO: Much faster than UTF-8 encoding
                // - CON: Hash values will differ from previous implementation using UTF-8.GetBytes()
                // - CON: Dependent on platform endianness (little vs big endian architectures), although
                //        all the major modern platforms—Windows (both Intel64 and ARM64), macOS, iOS (iPhone),
                //        and Android—use little-endian representations.  
                // - CON: Uses UTF-16 representation, which is less consistent for international strings
                //        than UTF-8 encoding
                return MurmurHash3.Hash32(MemoryMarshal.AsBytes(memory.Span), seed);
            }
            else
            {
                // For non-string types, use GetHashCode and then MurmurHash3
                // - PRO: Potentially more uniform distribution
                // - CON: see below
                //
                //int hashCode = key.GetHashCode();
                //byte[] bytes = BitConverter.GetBytes(hashCode);
                //return MurmurHash3.Hash32(bytes, seed);

                // using GetHashCode() directly
                // For most practical use cases, this distribution is sufficient
                // - PRO: Avoids all allocations and additional hashing overhead
                // - PRO: Takes advantage of the already optimized GetHashCode() implementations
                //        in .NET types
                // - PRO: Much simpler and more efficient
                // - CON: Potentially less uniform distribution than running through MurmurHash3
                //        (though GetHashCode() is generally well-distributed for most types)
                // - CON: GetHashCode() is not guaranteed to be stable across different .NET versions,
                //        so hash values could change with framework updates
                //
                return (uint)key.GetHashCode();
            }
        }

        public void Insert(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (count >= capacity * 0.9)
                throw new InvalidOperationException("Hash table is too full. Consider increasing capacity.");

            uint hash = Hash(key) % (uint)capacity;
            int segmentStart = (int)(hash / segmentSize) * segmentSize;

            // First try within the segment
            for (int i = 0; i < segmentSize; i++)
            {
                int index = (segmentStart + QuadraticProbe(i)) % capacity;

                if (table[index] == null)
                {
                    table[index] = new Entry(key, value);
                    count++;
                    return;
                }
                else if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    table[index].Value = value; // Update existing value
                    return;
                }
                else if (!table[index].IsOccupied)
                {
                    table[index] = new Entry(key, value); // Reuse deleted slot
                    count++;
                    return;
                }
            }

            // If segment is full, extend search with quadratic probing
            int extendedProbeStart = segmentStart + segmentSize;
            for (int i = 0; i < capacity - segmentSize; i++)
            {
                int index = (extendedProbeStart + QuadraticProbe(i)) % capacity;

                if (table[index] == null)
                {
                    table[index] = new Entry(key, value);
                    count++;
                    return;
                }
                else if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    table[index].Value = value; // Update existing value
                    return;
                }
                else if (!table[index].IsOccupied)
                {
                    table[index] = new Entry(key, value); // Reuse deleted slot
                    count++;
                    return;
                }
            }

            throw new InvalidOperationException("No available slot found despite load factor check. This is likely a bug.");
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            uint hash = Hash(key) % (uint)capacity;
            int segmentStart = (int)(hash / segmentSize) * segmentSize;

            // First search within the segment
            for (int i = 0; i < segmentSize; i++)
            {
                int index = (segmentStart + QuadraticProbe(i)) % capacity;

                if (table[index] == null)
                {
                    value = default;
                    return false;
                }

                if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    value = table[index].Value;
                    return true;
                }
            }

            // If not found in segment, extend search
            int extendedProbeStart = segmentStart + segmentSize;
            for (int i = 0; i < capacity - segmentSize; i++)
            {
                int index = (extendedProbeStart + QuadraticProbe(i)) % capacity;

                if (table[index] == null)
                {
                    value = default;
                    return false;
                }

                if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    value = table[index].Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public TValue Get(TKey key)
        {
            if (TryGet(key, out TValue value))
                return value;
            return default;
        }

        public bool Delete(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            uint hash = Hash(key) % (uint)capacity;
            int segmentStart = (int)(hash / segmentSize) * segmentSize;

            // First search within the segment
            for (int i = 0; i < segmentSize; i++)
            {
                int index = (segmentStart + QuadraticProbe(i)) % capacity;

                if (table[index] == null)
                    return false;

                if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    table[index].IsOccupied = false; // Logical deletion
                    count--;
                    return true;
                }
            }

            // If not found in segment, extend search
            int extendedProbeStart = segmentStart + segmentSize;
            for (int i = 0; i < capacity - segmentSize; i++)
            {
                int index = (extendedProbeStart + QuadraticProbe(i)) % capacity;

                if (table[index] == null)
                    return false;

                if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    table[index].IsOccupied = false; // Logical deletion
                    count--;
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int QuadraticProbe(int probeCount)
        {
            return (probeCount * probeCount + probeCount) / 2; // Improved quadratic probing sequence
        }

        public int Count => count;
    }
}