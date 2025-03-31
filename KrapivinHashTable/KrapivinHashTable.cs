using MurmurHash.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KrapivinHashTable
{
    public class KrapivinHashTable<TKey, TValue> :
        IEnumerable<KeyValuePair<TKey, TValue>>,
        ICollection<KeyValuePair<TKey, TValue>>,
        IReadOnlyCollection<KeyValuePair<TKey, TValue>>,
        IDictionary<TKey, TValue>,
        IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly int capacity;
        private readonly int segmentSize;
        private readonly Entry[] table;
        private readonly IEqualityComparer<TKey> comparer;

        public int Count { get; private set; }

        public bool IsReadOnly => false;

        public ICollection<TKey> Keys
            => table.Where(e => e != null && e.IsOccupied).Select(e => e.Key).ToArray();

        public ICollection<TValue> Values 
            => table.Where(e => e != null && e.IsOccupied).Select(e => e.Value).ToArray();

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

        public TValue this[TKey key]
        { 
            get => Get(key);
            set => Insert(key, value);
        }

        private sealed class Entry
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

            internal KeyValuePair<TKey, TValue> ToKeyValuePair()
                => new KeyValuePair<TKey, TValue>(Key, Value);
        }

        public KrapivinHashTable(int initialCapacity = 1024, int segmentSize = 32, IEqualityComparer<TKey> comparer = null)
        {
            if (initialCapacity < segmentSize || segmentSize <= 0)
                throw new ArgumentException("Invalid capacity or segment size.");

            this.capacity = initialCapacity;
            this.segmentSize = segmentSize;
            this.table = new Entry[capacity];
            Count = 0;
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

        private void Insert(TKey key, TValue value, bool throwWehnExists = false)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (Count >= capacity * 0.9)
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
                    Count++;
                    return;
                }
                else if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    if (throwWehnExists)
                        throw new ArgumentException("An item with the same key has already been added.");

                    table[index].Value = value; // Update existing value
                    return;
                }
                else if (!table[index].IsOccupied)
                {
                    table[index] = new Entry(key, value); // Reuse deleted slot
                    Count++;
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
                    Count++;
                    return;
                }
                else if (table[index].IsOccupied && comparer.Equals(table[index].Key, key))
                {
                    if (throwWehnExists)
                        throw new ArgumentException("An item with the same key has already been added.");

                    table[index].Value = value; // Update existing value
                    return;
                }
                else if (!table[index].IsOccupied)
                {
                    table[index] = new Entry(key, value); // Reuse deleted slot
                    Count++;
                    return;
                }
            }

            throw new InvalidOperationException("No available slot found despite load factor check. This is likely a bug.");
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
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
            if (TryGetValue(key, out TValue value))
                return value;
            return default;
        }

        private bool Delete(TKey key)
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
                    Count--;
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
                    Count--;
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

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var item in table)
            {
                yield return item.ToKeyValuePair();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item)
            => Insert(item.Key, item.Value, throwWehnExists: true);

        public void Clear()
        {
            Array.Clear(table, 0, table.Length);
            Count = 0;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return TryGetValue(item.Key, out TValue value)
                && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            for (int i=arrayIndex; i<table.Length; i++)
            {
                array[i] = table[i].ToKeyValuePair();
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (TryGetValue(item.Key, out TValue value)
                && EqualityComparer<TValue>.Default.Equals(value, item.Value))
            {
                Delete(item.Key);
                return true;
            }
            return false;
        }

        public bool ContainsKey(TKey key) 
            => TryGetValue(key, out _);

        public void Add(TKey key, TValue value)
            => Insert(key, value, throwWehnExists: true);

        public bool Remove(TKey key)
            => Delete(key);
    }
}