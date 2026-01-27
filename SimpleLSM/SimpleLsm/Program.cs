using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleLSM
{
    // The "Engine" combining WAL and Memory
    public class KVStore
    {
        private const string WalFilePath = "wal.log";
        private Dictionary<string, string> _memTable;

        public KVStore()
        {
            _memTable = new Dictionary<string, string>();

            // On startup, we always try to recover!
            if (File.Exists(WalFilePath))
            {
                RecoverFromWal();
            }
        }

        // 1. THE WRITE PATH
        public void Put(string key, string value)
        {
            // Step A: Append to Disk (WAL) - Durability First!
            // We use 'true' for append mode.
            using (StreamWriter sw = new StreamWriter(WalFilePath, true))
            {
                // Format: SET,key,value
                string logEntry = $"SET,{key},{value}";
                sw.WriteLine(logEntry);
            }

            // Step B: Write to Memory
            if (_memTable.ContainsKey(key))
            {
                _memTable[key] = value;
            }
            else
            {
                _memTable.Add(key, value);
            }

            Console.WriteLine($"[Write] Key: {key} saved to WAL and RAM.");
        }

        public string Get(string key)
        {
            if (_memTable.ContainsKey(key))
            {
                return _memTable[key];
            }
            return null;
        }

        // 2. THE CRASH SIMULATION
        public void SimulateCrash()
        {
            Console.WriteLine("\n!!! SYSTEM CRASHING !!! Power loss...");
            Console.WriteLine("!!! RAM is being wiped... !!!\n");
            _memTable.Clear(); // Data is gone from memory!
        }

        // 3. THE RECOVERY
        public void RecoverFromWal()
        {
            Console.WriteLine("[Recovery] Scanning WAL to rebuild memory...");

            if (!File.Exists(WalFilePath)) return;

            var lines = File.ReadAllLines(WalFilePath);
            foreach (var line in lines)
            {
                // Parse our simple CSV format
                var parts = line.Split(',');
                if (parts.Length >= 3 && parts[0] == "SET")
                {
                    string key = parts[1];
                    string value = parts[2];

                    // Replay the insert into memory
                    if (_memTable.ContainsKey(key))
                        _memTable[key] = value;
                    else
                        _memTable.Add(key, value);
                }
            }
            Console.WriteLine($"[Recovery] Complete. Restored {_memTable.Count} keys.\n");
        }

        // 4. THE PURGE (Explained below)
        public void PurgeWal()
        {
            // In a real system, we only do this AFTER flushing RAM to an SSTable.
            // Since we are simulating, we just delete the file.
            File.Delete(WalFilePath);
            Console.WriteLine("[Maintenance] WAL has been truncated/purged.");
        }

        // Helper to show current state
        public void PrintMemoryState()
        {
            Console.WriteLine("--- Current Memory State ---");
            if (_memTable.Count == 0) Console.WriteLine("(Empty)");
            foreach (var kvp in _memTable)
            {
                Console.WriteLine($"Key: {kvp.Key}, Value: {kvp.Value}");
            }
            Console.WriteLine("----------------------------");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Clean up old runs
            if (File.Exists("wal.log")) File.Delete("wal.log");

            KVStore store = new KVStore();

            // 1. Normal Usage
            store.Put("user1", "Alice");
            store.Put("user2", "Bob");
            store.Put("user3", "Charlie");

            store.PrintMemoryState();

            // 2. The Disaster
            store.SimulateCrash();
            store.PrintMemoryState(); // Should be empty

            // 3. The Recovery
            // In real life, this happens when you restart the application (new KVStore())
            // Here we call the method manually to simulate the restart.
            store.RecoverFromWal();
            store.PrintMemoryState(); // Should have Alice, Bob, Charlie back!

            Console.ReadLine();
        }
    }
}