using System;
using System.Diagnostics;

namespace SimpleTrieApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Setup
            Trie trie = new Trie();
            string dictionaryPath = @"words_alpha.txt";

            Console.WriteLine("--- Simple Trie Performance Test ---");
            Console.WriteLine($"Reading from: {dictionaryPath}");

            // 2. Load & Measure Time
            Stopwatch sw = Stopwatch.StartNew();

            // Force Garbage Collection before starting to get a clean baseline
            long initialMemory = GC.GetTotalMemory(true);

            try
            {
                trie.LoadDictionary(dictionaryPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            sw.Stop();
            long finalMemory = GC.GetTotalMemory(true);
            long memoryUsed = finalMemory - initialMemory;

            // 3. Output Stats
            Console.WriteLine("\n--- Loading Complete ---");
            Console.WriteLine($"Words Loaded: {trie.WordCount:N0}");
            Console.WriteLine($"Time Taken:   {sw.ElapsedMilliseconds} ms");

            // Convert Bytes to Megabytes
            double mbUsed = memoryUsed / 1024.0 / 1024.0;
            Console.WriteLine($"Memory Used:  ~{mbUsed:F2} MB");

            Console.WriteLine("\n--- Interactive Word Lookup ---");
            Console.WriteLine("Type a word to search for it.");
            Console.WriteLine("Type 'exit' to quit.\n");

            while (true)
            {
                Console.Write("Enter word: ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    break;

                TestWord(trie, input);
            }
        }

        static void TestWord(Trie t, string word)
        {
            bool found = t.Search(word);
            string status = found ? "[FOUND]" : "[MISSING]";
            Console.WriteLine($"{status} : {word}");
        }
    }
}