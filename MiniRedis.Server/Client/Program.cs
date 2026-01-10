using System.Text;
using System.Text.Json;

// 1. SETUP THE RING
var hashRing = new ConsistentHashRing();

// Add our 3 servers to the ring
// Note: In a real system, we might add "Virtual Nodes" (Server1_A, Server1_B)
// to spread data more evenly, but we'll stick to simple nodes for now.
hashRing.AddNode("http://localhost:5000/cache");
hashRing.AddNode("http://localhost:5001/cache");
hashRing.AddNode("http://localhost:5002/cache");

using HttpClient client = new HttpClient();

Console.WriteLine("--- Distributed Mini-Redis (Consistent Hashing) ---");
Console.WriteLine("Ring initialized with 3 nodes.");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;

    var parts = input.Split(' ');
    var command = parts[0].ToLower();

    // Command to simulate adding a server dynamically
    if (command == "add-node")
    {
        // Example: add-node http://localhost:5003/cache
        var newNode = parts[1];
        hashRing.AddNode(newNode);
        Console.WriteLine($"Node {newNode} added to the ring!");
        continue;
    }

    var key = parts.Length > 1 ? parts[1] : "";

    // 2. GET TARGET SERVER FROM RING
    string targetServer = hashRing.GetNode(key);

    // Visualizing the decision
    Console.WriteLine($"[Ring Router] Key '{key}' --> {targetServer}");

    try
    {
        if (command == "set" && parts.Length == 3)
        {
            await SendSetAsync(targetServer, key, parts[2]);
        }
        else if (command == "get" && parts.Length == 2)
        {
            await SendGetAsync(targetServer, key);
        }
    }
    catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
}

// --- HTTP Helpers (No changes here) ---
async Task SendSetAsync(string url, string key, string value)
{
    var json = JsonSerializer.Serialize(new { key, value });
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await client.PostAsync(url, content);
    Console.WriteLine(response.IsSuccessStatusCode ? "Success: OK" : "Failed");
}

async Task SendGetAsync(string url, string key)
{
    var response = await client.GetAsync($"{url}/{key}");
    if (response.IsSuccessStatusCode)
    {
        var val = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Result: {val}");
    }
    else
    {
        Console.WriteLine("Result: <Not Found>");
    }
}