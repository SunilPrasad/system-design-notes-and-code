using System;
using System.Collections; // For BitArray
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LSM_Full_Implementation
{
    // ==========================================
    // 1. BLOOM FILTER (The "Guard")
    // ==========================================
    public class BloomFilter
    {
        private BitArray _bitArray;
        private int _size;

        public BloomFilter(int size)
        {
            _size = size;
            _bitArray = new BitArray(size);
        }

        // Helper to generate 3 hash positions
        private int[] GetHashPositions(string item)
        {
            int hash1 = Math.Abs((item + "1").GetHashCode()) % _size;
            int hash2 = Math.Abs((item + "2").GetHashCode()) % _size;
            int hash3 = Math.Abs((item + "3").GetHashCode()) % _size;
            return new int[] { hash1, hash2, hash3 };
        }

        public void Add(string item)
        {
            foreach (var pos in GetHashPositions(item))
                _bitArray[pos] = true;
        }

        public bool MightContain(string item)
        {
            foreach (var pos in GetHashPositions(item))
            {
                if (!_bitArray[pos]) return false; // Definitely not here
            }
            return true; // Maybe here
        }
    }

    // ==========================================
    // 2. THE STORAGE ENGINE (LSM Store)
    // ==========================================
    public class LSMStore
    {
        // CONSTANTS (Small values for demo purposes)
        private const string WalFilePath = "wal.log";
        private const string SstableFilePath = "data_1.sst";
        private const int MemTableLimit = 5;       // Flush after 5 items
        private const int BlockSize = 64;          // Start new block every 64 bytes
        private const int BloomFilterSize = 100;   // Size of bit array

        // MEMORY COMPONENTS
        private Dictionary<string, string> _memTable;

        // DISK HELPERS (Metadata loaded in RAM)
        private List<Tuple<string, long>> _sparseIndex; // (StartKey -> FileOffset)
        private Dictionary<long, string> _blockCache;   // (FileOffset -> RawBlockData)
        private BloomFilter _bloomFilter;

        public LSMStore()
        {
            _memTable = new Dictionary<string, string>();
            _sparseIndex = new List<Tuple<string, long>>();
            _blockCache = new Dictionary<long, string>();
            _bloomFilter = new BloomFilter(BloomFilterSize);

            // Startup Logic
            if (File.Exists(SstableFilePath)) RebuildIndices(); // Load metadata from disk file
            if (File.Exists(WalFilePath)) RecoverFromWal();     // Restore crash data
        }

        // ------------------------------------------------
        // WRITE PATH: WAL -> MemTable -> (Maybe Flush)
        // ------------------------------------------------
        public void Put(string key, string value)
        {
            // 1. Append to WAL (Durability)
            using (StreamWriter sw = new StreamWriter(WalFilePath, true))
            {
                sw.WriteLine($"SET,{key},{value}");
            }

            // 2. Write to MemTable (RAM)
            if (_memTable.ContainsKey(key)) _memTable[key] = value;
            else _memTable.Add(key, value);

            Console.WriteLine($"[Write] '{key}' saved to RAM + WAL.");

            // 3. Check Flush Condition
            if (_memTable.Count >= MemTableLimit)
            {
                Flush();
            }
        }

        // ------------------------------------------------
        // FLUSH PATH: MemTable -> Sort -> SSTable (Blocks)
        // ------------------------------------------------
        private void Flush()
        {
            Console.WriteLine("\n[Flush] MemTable full! Sorting & Writing to Disk...");

            var sortedKeys = _memTable.Keys.OrderBy(k => k).ToList();

            // Clear old metadata (Since we are overwriting the single file for this demo)
            _sparseIndex.Clear();
            _blockCache.Clear();
            _bloomFilter = new BloomFilter(BloomFilterSize);

            using (var fs = new FileStream(SstableFilePath, FileMode.Create))
            using (var writer = new StreamWriter(fs, Encoding.UTF8))
            {
                long currentBlockBytes = 0;

                // Initialize Index for the very first block
                if (sortedKeys.Count > 0)
                {
                    _sparseIndex.Add(new Tuple<string, long>(sortedKeys[0], 0));
                }

                foreach (var key in sortedKeys)
                {
                    string value = _memTable[key];
                    string line = $"{key},{value}\n";
                    int lineBytes = Encoding.UTF8.GetByteCount(line);

                    // A. Add to Bloom Filter
                    _bloomFilter.Add(key);

                    // B. Block Management (Size-Based)
                    // If adding this line exceeds the block limit, we start a new block.
                    if (currentBlockBytes + lineBytes > BlockSize && currentBlockBytes > 0)
                    {
                        writer.Flush(); // Force write to ensure accurate position
                        long filePosition = fs.Position;

                        _sparseIndex.Add(new Tuple<string, long>(key, filePosition));
                        Console.WriteLine($"   -> New Block: Starts with '{key}' at byte {filePosition}");

                        currentBlockBytes = 0; // Reset counter for new block
                    }

                    // C. Write Data
                    writer.Write(line);
                    currentBlockBytes += lineBytes;
                }
            }

            // Cleanup
            _memTable.Clear();
            File.Delete(WalFilePath); // Safe to delete WAL now
            Console.WriteLine("[Flush] Done. WAL purged.\n");
        }

        // ------------------------------------------------
        // READ PATH: MemTable -> Bloom -> Index -> Cache -> Disk
        // ------------------------------------------------
        public string Get(string key)
        {
            // 1. Check MemTable (Fastest)
            if (_memTable.ContainsKey(key))
            {
                Console.WriteLine($"[Read] '{key}' found in MemTable.");
                return _memTable[key];
            }

            // If no file exists, we can't do anything else
            if (!File.Exists(SstableFilePath)) return null;

            // 2. Check Bloom Filter (The Guard)
            if (!_bloomFilter.MightContain(key))
            {
                Console.WriteLine($"[Read] Bloom Filter says '{key}' is NOT on disk. Skipping I/O.");
                return null;
            }

            // 3. Sparse Index Search (Find the candidate block)
            long blockOffset = -1;
            foreach (var entry in _sparseIndex)
            {
                // We look for the start key that is <= our key
                if (string.Compare(entry.Item1, key) <= 0)
                {
                    blockOffset = entry.Item2;
                }
                else
                {
                    break; // Passed the target
                }
            }

            if (blockOffset == -1) return null; // Should not happen if Bloom said yes (unless false positive)

            // 4. Block Cache Check
            if (_blockCache.ContainsKey(blockOffset))
            {
                Console.WriteLine($"[Read] Block found in Cache (Offset {blockOffset}).");
                return SearchBlock(_blockCache[blockOffset], key);
            }

            // 5. Disk Read (The slow part)
            Console.WriteLine($"[Read] Cache MISS. Reading Disk at Offset {blockOffset}...");
            string rawBlock = ReadBlockFromDisk(blockOffset);

            // Store in Cache
            _blockCache[blockOffset] = rawBlock;

            return SearchBlock(rawBlock, key);
        }

        // Helper: Parse the block string to find the key
        private string SearchBlock(string blockData, string key)
        {
            var lines = blockData.Split('\n');
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && parts[0] == key)
                    return parts[1];
            }
            return null; // False positive from Bloom Filter?
        }

        // Helper: Read from disk until the next block starts or EOF
        private string ReadBlockFromDisk(long offset)
        {
            // In a real DB, we read exactly 4KB. 
            // Here, we simulate by reading 100 bytes or until EOF.
            using (var fs = new FileStream(SstableFilePath, FileMode.Open))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                byte[] buffer = new byte[100]; // Simulated "Block Read" size
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }
        }

        // ------------------------------------------------
        // RECOVERY & STARTUP
        // ------------------------------------------------
        private void RecoverFromWal()
        {
            Console.WriteLine("[Startup] Recovering from WAL...");
            var lines = File.ReadAllLines(WalFilePath);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 3 && parts[0] == "SET")
                {
                    if (_memTable.ContainsKey(parts[1])) _memTable[parts[1]] = parts[2];
                    else _memTable.Add(parts[1], parts[2]);
                }
            }
            Console.WriteLine($"[Startup] Restored {_memTable.Count} items from WAL.");
        }

        // In a real DB, the Sparse Index is saved to a file (index.db).
        // Here, we quickly scan the SSTable to rebuild it in RAM on startup.
        private void RebuildIndices()
        {
            Console.WriteLine("[Startup] Rebuilding Indices from SSTable...");
            using (var fs = new FileStream(SstableFilePath, FileMode.Open))
            using (var sr = new StreamReader(fs))
            {
                long currentBlockBytes = 0;
                string line;
                long filePos = 0;

                // Add first block
                // (Simplified logic: In real usage, we would scan bytes carefully.
                // For this demo, we assume the previous Flush logic holds.)
                // To keep it simple: We just re-flush or start fresh in this demo.
                // But normally this function reads the file to fill _sparseIndex and _bloomFilter.
            }
            // Note: For this demo code, simply deleting the file on start or relying on Flush
            // is easier than writing a complex parser. We will rely on runtime persistence.
        }

        // Debug helper
        public void Crash()
        {
            Console.WriteLine("\n!!! SIMULATING CRASH !!!");
            _memTable.Clear();
            _blockCache.Clear();
            // Note: We do NOT clear the WAL. That's the point.
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // Cleanup previous runs for a clean demo
            if (File.Exists("wal.log")) File.Delete("wal.log");
            if (File.Exists("data_1.sst")) File.Delete("data_1.sst");

            LSMStore store = new LSMStore();

            Console.WriteLine("--- PHASE 1: Filling Memory ---");
            store.Put("apple", "red");
            store.Put("banana", "yellow");
            store.Put("cherry", "red");
            store.Put("date", "brown");

            // This next one will trigger FLUSH (Limit is 5)
            // Items: apple, banana, cherry, date, elderberry -> 5 items
            store.Put("elderberry", "purple");

            Console.WriteLine("\n--- PHASE 2: Reading from Disk (SSTable) ---");
            // 1. Read something that exists
            // Should hit Index -> Disk -> Cache
            var res1 = store.Get("banana");
            Console.WriteLine($"Result: {res1}\n");

            // 2. Read something again (Cache Hit)
            // Should hit Index -> Cache (No Disk)
            var res2 = store.Get("apple"); // 'apple' is in the same block as 'banana'
            Console.WriteLine($"Result: {res2}\n");

            // 3. Read something that doesn't exist (Bloom Filter)
            // Should stop at Bloom Filter (No Index, No Disk)
            store.Get("zucchini");

            Console.WriteLine("\n--- PHASE 3: Crash & Recovery ---");
            // Add new data to MemTable (but don't flush yet)
            store.Put("fig", "green");
            store.Put("grape", "purple");

            // Simulate Power Failure
            store.Crash();

            // Restart System
            // This simulates "new LSMStore()"
            Console.WriteLine("\n... Restarting System ...");
            LSMStore newSession = new LSMStore();

            // Check if 'fig' survived via WAL
            string recovered = newSession.Get("fig");
            Console.WriteLine($"Recovered 'fig': {recovered}");

            Console.ReadLine();
        }
    }
}