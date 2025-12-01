using System.Text.Json;
using SseStocksApi.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS: allow Angular dev server
var angularOrigin = "http://localhost:4200";

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()       // allow all domains
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});


// Register our stock price service as a singleton
builder.Services.AddSingleton<StockPriceService>();

var app = builder.Build();

app.UseCors("AllowAll");

// Simple health check
app.MapGet("/", () => "SSE Stocks API is running");

// SSE endpoint
app.MapGet("/stocks/stream", async (HttpContext context, StockPriceService stockService) =>
{
    var response = context.Response;

    response.Headers.Add("Content-Type", "text/event-stream");
    response.Headers.Add("Cache-Control", "no-cache");
    response.Headers.Add("Connection", "keep-alive");

    // Optional: disable buffering in some reverse proxies (nginx etc.)
    response.Headers.TryAdd("X-Accel-Buffering", "no");

    var cancellationToken = context.RequestAborted;

    while (!cancellationToken.IsCancellationRequested)
    {
        var stocks = stockService.GetUpdatedPrices();

        var json = JsonSerializer.Serialize(stocks, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // SSE format: "data: <payload>\n\n"
        await response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);

        try
        {
            // Wait 1 second between updates
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Client disconnected
            break;
        }
    }
});

app.Run();
