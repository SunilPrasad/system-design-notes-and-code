**What is a Bloom Filter?**

A Bloom Filter is a space-efficient, probabilistic data structure used to test whether an element is a member of a set.

Think of it as a "very compact, fast, but slightly fuzzy" hash set. It tells you one of two things:

"No": The item is definitely not in the set.

"Maybe": The item is probably in the set (with a small chance it's wrong, called a "false positive").

**Why use it?**

It is used when you need to check if something exists without storing the actual data (which might be massive).

- Space Savings: You can represent a set of 1 million items in just a few megabytes.

- Speed: Operations are $O(k)$ (constant time), faster than disk lookups.

- No False Negatives: If it says "No", you can trust it 100%

**Common Use Cases:**

- Username Checks: Checking if a username is taken without querying the main DB.

- Databases: To quickly check if a record exists on disk before doing an expensive read operation (e.g., Cassandra, HBase).

- Browsers: To check if a URL is in a list of known malicious sites.

How it Works (The Math)
1. Bit Array: It starts as a large array of bits (0s), all set to 0.
2. Hashing: You need k different hash functions.
3. Adding an Item
   - Run the item through all k hash functions
   - Each function gives you an index
   - Set the bits at those indices to 1
4. Checking an Item
  - Run the item through the same k hash functions.
  - Check the bits at the resulting indices.
  - If any bit is 0: The item is definitely not there.
  = If all bits are 1: The item is probably there (it could be a collision where other items happened to set those same bits).


## 📐 Optimal Size Calculation

To determine the most efficient size for the Bloom Filter, we don't guess. We calculate the exact size required to minimize memory usage while adhering to a specific **False Positive Rate**.

### 1. The Inputs
To calculate the size, you need two values:
* **`n`**: The number of items you expect to store (e.g., `1,000,000`).
* **`p`**: The acceptable error rate (e.g., `0.01` for 1%).

### 2. The Formula
The standard formula to calculate the BitArray size (`m`) is:

```math

m = - (n * ln(p)) / (ln(2)^2)

```

***Example Scenario***

Let's calculate the size required to store 1 Million Usernames with a 1% chance of error

- n = 1,000,000
- p = 0.01

The Math:

- ln(0.01) $\approx$ -4.605
- -(1,000,000 * -4.605) = 4,605,170
- 4,605,170 / 0.48045 = 9,585,058 bits

The Result:Bits: ~9.6 Million bits

Memory: ~1.14 MB

## 🔢 Optimal Hash Count (k)

You might wonder: *"Why do we use 7 hash functions? Why not just 1 or 100?"*

The number of hash functions ($k$) is not random. It is calculated to minimize the error rate for a specific array size.

### 1. The Logic (The "Goldilocks" Rule)
* **Too Few Hashes ($k=1$):** Not enough differentiation. Items collide too easily.
* **Too Many Hashes ($k=100$):** You set too many bits to `1` for every item. The array fills up instantly, causing errors.
* **The Sweet Spot:** The math proves that the filter is most efficient when the bit array is exactly **50% full**. To achieve this, we must calculate the specific number of hashes.

### 2. The Formula
The optimal number of hash functions is roughly **70% of the bits-per-item**.

```math

k = (m / n) * ln(2)

```

## 🧮 The Math: Double Hashing Strategy

To generate `k` positions for an item, this implementation uses the **Kirsch-Mitzenmacher Optimization** (2006).

Instead of computing `k` expensive hash functions (like MD5 or SHA) separately, we compute two 32-bit hash values ($h_1$ and $h_2$) and generate the remaining positions using this linear formula:

```csharp
index = (h1 + i * h2) % m

```

## 🔐 Hashing Algorithm: FNV-1a

This implementation uses the **Fowler–Noll–Vo (FNV-1a)** hash algorithm rather than the default `.GetHashCode()`.

### Why FNV-1a?
1.  **Speed:** It uses only XOR and Multiplication operations, making it extremely fast for high-performance loops.
2.  **Distribution:** It has excellent avalanche properties (small changes in input result in massive changes in the hash).
3.  **Stability:** Unlike `.GetHashCode()`, FNV-1a is deterministic. The same string always results in the same hash, regardless of the .NET version or server uptime.

### The Constants
The code uses standard 32-bit FNV constants:
* **Prime:** `16777619`
* **Offset Basis:** `2166136261`

To achieve **Double Hashing**, we run the algorithm twice with different initial seeds (the standard Offset Basis for Hash A, and a custom seed for Hash B) to generate two independent hash values.