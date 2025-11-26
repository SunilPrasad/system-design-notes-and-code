using System;
using System.Collections; // Required for BitArray
using System.Text;


public class BloomFilter<T>
{
    private readonly BitArray _bitArray;
    private readonly int _hashCount; // 'k'
    private readonly int _size;      // 'm'

    public int Size => _size;
    public int HashCount => _hashCount;

    /// <summary>
    /// Initializes the Bloom Filter.
    /// Automatically calculates optimal size (m) and hash count (k).
    /// </summary>
    /// <param name="capacity">Expected number of elements (n).</param>
    /// <param name="errorRate">Acceptable false positive rate (p) (e.g. 0.01).</param>
    public BloomFilter(int capacity, double errorRate = 0.01)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be > 0");
        if (errorRate <= 0 || errorRate >= 1) throw new ArgumentOutOfRangeException(nameof(errorRate), "Error rate must be between 0 and 1");

        // 1. Calculate optimal size (m)        
        double m = -(capacity * Math.Log(errorRate)) / (Math.Log(2) * Math.Log(2));
        _size = (int)Math.Ceiling(m);

        // 2. Calculate optimal number of hash functions (k)
        // Formula: k = (m / n) * ln(2)
        double k = (_size / (double)capacity) * Math.Log(2);
        _hashCount = (int)Math.Ceiling(k);

        // 3. Initialize the BitArray
        _bitArray = new BitArray(_size);
    }

    /// <summary>
    /// Adds an item to the filter.
    /// </summary>
    public void Add(T item)
    {
        var (hash1, hash2) = GetHashValues(item);

        for (int i = 0; i < _hashCount; i++)
        {
            // Double Hashing Formula: (h1 + i * h2) % m
            int index = Math.Abs((hash1 + i * hash2) % _size);
            _bitArray[index] = true;
        }
    }

    /// <summary>
    /// Checks if an item exists in the filter.
    /// </summary>
    /// <returns>
    /// False: Definitely NOT in the set.
    /// True: PROBABLY in the set.
    /// </returns>
    public bool Contains(T item)
    {
        var (hash1, hash2) = GetHashValues(item);

        for (int i = 0; i < _hashCount; i++)
        {
            int index = Math.Abs((hash1 + i * hash2) % _size);

            // If any bit is 0, the item was definitely never added.
            if (!_bitArray[index])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Generates two distinct hash values using FNV-1a.
    /// This allows us to simulate 'k' hash functions without calculating 'k' hashes.
    /// </summary>
    private (int hash1, int hash2) GetHashValues(T item)
    {
        string input = item?.ToString() ?? string.Empty;

        // FNV-1a Constants (32-bit)
        const uint FnvPrime = 16777619;
        const uint OffsetBasis = 2166136261;

        // Hash A
        int hash1 = Fnv1aHash(input, OffsetBasis, FnvPrime);

        // Hash B (Same algo, different seed/basis to make it independent)
        // We simply flip the basis to generate a second unique hash
        int hash2 = Fnv1aHash(input, 123456789, FnvPrime);

        return (hash1, hash2);
    }

    private int Fnv1aHash(string str, uint hash, uint prime)
    {
        foreach (char c in str)
        {
            hash ^= c;
            hash *= prime;
        }
        return (int)hash;
    }
}