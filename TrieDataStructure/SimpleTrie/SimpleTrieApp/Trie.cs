using System;
using System.IO;

namespace SimpleTrieApp
{
    public class TrieNode
    {
        // 26 pointers per node (8 bytes each = 208 bytes just for pointers!)
        public TrieNode[] Children = new TrieNode[26];
        public bool IsEndOfWord = false;
    }

    public class Trie
    {
        private readonly TrieNode _root;
        public int WordCount { get; private set; }

        public Trie()
        {
            _root = new TrieNode();
            WordCount = 0;
        }

        public void Insert(string word)
        {
            TrieNode current = _root;
            foreach (char c in word.ToLower())
            {
                // Safety check: skip anything that isn't a-z
                if (c < 'a' || c > 'z') continue;

                int index = c - 'a';
                if (current.Children[index] == null)
                {
                    current.Children[index] = new TrieNode();
                }
                current = current.Children[index];
            }

            if (!current.IsEndOfWord)
            {
                current.IsEndOfWord = true;
                WordCount++;
            }
        }

        public bool Search(string word)
        {
            TrieNode current = _root;
            foreach (char c in word.ToLower())
            {
                if (c < 'a' || c > 'z') return false;

                int index = c - 'a';
                if (current.Children[index] == null)
                {
                    return false;
                }
                current = current.Children[index];
            }
            return current.IsEndOfWord;
        }

        // NEW: Helper to load directly from a file path
        public void LoadDictionary(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found at {filePath}");
                return;
            }

            // ReadLines is memory efficient (reads one by one)
            foreach (string line in File.ReadLines(filePath))
            {
                // Trim removes whitespace/newlines
                string cleanWord = line.Trim();
                if (!string.IsNullOrEmpty(cleanWord))
                {
                    Insert(cleanWord);
                }
            }
        }
    }
}