using System.Security.Cryptography;
using System.Text;

public class ConsistentHashRing
{
    // The "Ring": Stores Hash -> ServerURL
    // We use a SortedDictionary so we can search it quickly.
    private readonly SortedDictionary<int, string> _ring = new();

    // Adds a server to the ring
    public void AddNode(string nodeUrl)
    {
        int hash = CalculateHash(nodeUrl);
        _ring[hash] = nodeUrl;
    }

    // Removes a server from the ring
    public void RemoveNode(string nodeUrl)
    {
        int hash = CalculateHash(nodeUrl);
        if (_ring.ContainsKey(hash))
        {
            _ring.Remove(hash);
        }
    }

    // The Core Logic: Find which node owns this key
    public string GetNode(string key)
    {
        if (_ring.Count == 0) return null;

        int keyHash = CalculateHash(key);

        // 1. Try to find a node with a hash >= keyHash (Clockwise search)
        // We look for the first node "ahead" of us on the ring.
        foreach (var nodeHash in _ring.Keys)
        {
            if (nodeHash >= keyHash)
            {
                return _ring[nodeHash];
            }
        }

        // 2. Wrap Around
        // If we are past the last node (hash is very high),
        // the "next" node is the FIRST one in the ring.
        return _ring.First().Value;
    }

    // A stable hash function (MD5)
    // We avoid string.GetHashCode() because it changes every time you restart the app.
    private int CalculateHash(string input)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(bytes);

        // Convert the first 4 bytes of the hash into an integer
        return BitConverter.ToInt32(hashBytes, 0);
    }
}