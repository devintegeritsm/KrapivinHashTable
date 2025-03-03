using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MurmurHash;
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
        private uint Hash(TKey key)
        {
            byte[] data = key switch
            {
                string str => System.Text.Encoding.UTF8.GetBytes(str),
                _ => BitConverter.GetBytes(key.GetHashCode()) // Fallback for non-string types
            };
            return MurmurHash3.Hash32(data, 0); // Default seed of 0
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