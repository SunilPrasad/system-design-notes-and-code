using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json; // Used for converting text to objects

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

// THE PHONEBOOK: Maps "Alice" -> Her WebSocket Connection
var clientDirectory = new ConcurrentDictionary<string, WebSocket>();

app.Map("/chat", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        // 1. Identify the User (e.g., ws://server/chat?id=Alice)
        string userId = context.Request.Query["id"];

        if (string.IsNullOrEmpty(userId) || clientDirectory.ContainsKey(userId))
        {
            context.Response.StatusCode = 400; // ID missing or already taken
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        // 2. Add to Phonebook
        clientDirectory.TryAdd(userId, webSocket);
        Console.WriteLine($"[+] {userId} joined. Total: {clientDirectory.Count}");

        // 3. Start Listening Loop
        await HandleUserLoop(userId, webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

await app.RunAsync();

// The logic to route messages
async Task HandleUserLoop(string senderId, WebSocket senderSocket)
{
    var buffer = new byte[1024 * 4];

    try
    {
        while (senderSocket.State == WebSocketState.Open)
        {
            var result = await senderSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await senderSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
            }
            else
            {
                // 1. Decode the raw bytes into text
                var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    // 2. Parse the JSON (We expect: {"TargetId": "Bob", "Content": "Hello"})
                    var msgObj = JsonSerializer.Deserialize<MessagePacket>(jsonMessage);

                    if (msgObj != null && !string.IsNullOrEmpty(msgObj.TargetId))
                    {
                        // 3. ROUTING: Find the target
                        if (clientDirectory.TryGetValue(msgObj.TargetId, out WebSocket? targetSocket))
                        {
                            if (targetSocket.State == WebSocketState.Open)
                            {
                                // 4. Forward the message
                                // We wrap it in a new format so the receiver knows who sent it
                                string forwardPayload = $"From {senderId}: {msgObj.Content}";
                                byte[] forwardBytes = Encoding.UTF8.GetBytes(forwardPayload);

                                await targetSocket.SendAsync(new ArraySegment<byte>(forwardBytes), WebSocketMessageType.Text, true, CancellationToken.None);
                                Console.WriteLine($"[->] {senderId} sent to {msgObj.TargetId}");
                            }
                        }
                        else
                        {
                            // Target not found
                            string errorMsg = $"System: User '{msgObj.TargetId}' is not online.";
                            await senderSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(errorMsg)), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
                catch
                {
                    Console.WriteLine($"Error parsing message from {senderId}");
                }
            }
        }
    }
    finally
    {
        // Cleanup when they disconnect
        clientDirectory.TryRemove(senderId, out _);
        Console.WriteLine($"[-] {senderId} left.");
    }
}

// Simple class to define our message structure
public class MessagePacket
{
    public string TargetId { get; set; } = "";
    public string Content { get; set; } = "";
}