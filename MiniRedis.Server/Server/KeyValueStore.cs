using System.Collections.Concurrent;

namespace MiniRedis.Server
{
    // This class acts as our "Database"
    public class KeyValueStore
    {
        // ConcurrentDictionary handles thread safety for us automatically.
        // If two users write at the same time, it won't crash.
        private readonly ConcurrentDictionary<string, string> _store = new();

        public void Set(string key, string value)
        {
            // This adds the key if it doesn't exist, or updates it if it does.
            _store[key] = value;
        }

        public string? Get(string key)
        {
            // Try to get the value. If not found, it returns null.
            _store.TryGetValue(key, out var value);
            return value;
        }
    }
}