using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// CONFIGURATION
// ---------------------------------------------------------
// The list of backend servers we want to distribute load to.
// In a real world scenario, this might come from a config file or service discovery.
var backendServers = new List<string>
{
    "http://localhost:5001",
    "http://localhost:5002",
    "http://localhost:5003"
};

// GLOBAL STATE
// ---------------------------------------------------------
// Using a static HttpClient prevents socket exhaustion.
var httpClient = new HttpClient();
// A counter to keep track of Round Robin (whose turn is it?)
int requestCounter = 0;


// THE LOAD BALANCING LOGIC
// ---------------------------------------------------------
app.MapGet("/", async (HttpContext context) =>
{
    // 1. Determine which server to use (Round Robin Algorithm)
    // We use Interlocked to make this thread-safe in case multiple requests come at once.
    int currentRequestCount = Interlocked.Increment(ref requestCounter);

    // The modulo operator (%) wraps the counter back to 0 when it hits the limit
    int serverIndex = currentRequestCount % backendServers.Count;
    string targetServerUrl = backendServers[serverIndex];

    Console.WriteLine($"[Load Balancer] Routing Request #{currentRequestCount} to --> {targetServerUrl}");

    try
    {
        // 2. Proxy the request (The "Reverse Proxy" part)
        // OPTIMIZATION: HttpCompletionOption.ResponseHeadersRead
        // This tells HttpClient: "Don't wait for the entire body/content to download. 
        // Return control to me as soon as the headers are received."
        var responseMessage = await httpClient.GetAsync(targetServerUrl, HttpCompletionOption.ResponseHeadersRead);

        // 3. Stream the response directly (Piping)
        // OPTIMIZATION: Results.Stream
        // Instead of "ReadAsStringAsync" (which loads the whole response into memory),
        // we open a stream and pipe the bits directly to the user as they arrive.
        // This dramatically lowers latency (Time To First Byte) and memory usage.
        var stream = await responseMessage.Content.ReadAsStreamAsync();

        return Results.Stream(stream, statusCode: (int)responseMessage.StatusCode);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Could not reach {targetServerUrl}: {ex.Message}");
        return Results.Problem($"Server {targetServerUrl} is down!");
    }
});

Console.WriteLine("--> Load Balancer Started on http://localhost:9000");
app.Run();