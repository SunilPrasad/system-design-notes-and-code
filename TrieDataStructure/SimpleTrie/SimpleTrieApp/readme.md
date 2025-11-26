# High-Performance Trie (Prefix Tree) Implementation

A custom implementation of the Trie data structure in C#, designed to explore efficient string searching algorithms.

The primary goal of this project was to understand the mechanics of spelling error detection in word processing tool.

## 🎯 Learning Goals

* **Algorithmic Efficiency:** Comparing $O(L)$ Trie lookups against standard $O(N)$ linear searches.
* **Memory vs. Speed:** Analyzing how trading significant RAM (Space) allows for constant-time lookups (Speed).
* **C# Internals:** Observing object overhead and Garbage Collection pressure when creating millions of node instances.

## ⚡ Performance Analysis: The Space-Time Trade-off

The most significant finding in this project was visualizing the cost of object-based data structures in C#.

| Metric | Source File (Disk) | Trie (RAM) |
| :--- | :--- | :--- |
| **Size** | **~5 MB** | **~278 MB** |
| **Format** | Raw Text | Linked Node Objects |
| **Lookup Speed** | $O(N)$ (Slow) | $O(L)$ (Instant) |

### 1. Why the 55x Memory Expansion?
While the raw dictionary file is only **5 MB**, loading it into memory consumed **~278 MB**.
* **The Cause:** In C#, every character node is a class instance (`TrieNode`). This incurs overhead for Object Headers (16 bytes), `Dictionary` references, and child pointers.
* **The Benefit:** Despite the heavy memory usage, I achieved **instantaneous** lookup speeds ($<0.01ms$) that are completely decoupled from the dataset size.

### 2. Search Complexity
* **Linear Search (List):** To find a word, you must scan the list. If the list doubles in size, the search takes twice as long.
* **Trie Search:** To find the word "Apple", the algorithm takes exactly 5 steps (A → P → P → L → E). It does not matter if the dictionary contains 100 words or 1,000,000 words; the speed remains constant.

## 📂 Data Source

* **Input:** A comprehensive English dictionary file.
* **Raw Size:** ~5 MB
* **Content:** Newline-separated words.

## ⚙️ Architecture

The solution relies on a recursive node architecture.

```csharp
public class TrieNode 
{
    // A dictionary maps the next character to the next node
    public Dictionary<char, TrieNode> Children = new();
    
    // Marks the end of a valid dictionary word
    public bool IsEndOfWord; 
}