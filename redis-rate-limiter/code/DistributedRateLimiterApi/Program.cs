using StackExchange.Redis;
using Microsoft.AspNetCore.Mvc;

// --- 1. Service Registration and Builder Setup ---
var builder = WebApplication.CreateBuilder(args);

// Register the SlidingWindowRateLimiter as a Singleton service
// NOTE: Ensure your Redis is running or replace the connection string!
builder.Services.AddSingleton<SlidingWindowRateLimiter>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// --- 2. Custom Rate Limiting Middleware ---

const int Limit = 3;
const int WindowSeconds = 15;
// In a real app, this should be the user's IP, API Key, or User ID.
const string ClientIdentifier = "user_123";

app.Use(async (context, next) =>
{
    // Apply rate limiting only to paths starting with '/protected'
    if (context.Request.Path.StartsWithSegments("/protected"))
    {
        var limiter = context.RequestServices.GetRequiredService<SlidingWindowRateLimiter>();

        // Perform the atomic rate limit check
        bool allowed = await limiter.IsRequestAllowed(ClientIdentifier, Limit, WindowSeconds);

        if (!allowed)
        {
            // Request is DENIED (Rate limit exceeded)
            context.Response.StatusCode = 429; // Too Many Requests
            context.Response.ContentType = "text/plain";
            context.Response.Headers.Add("X-RateLimit-Limit", Limit.ToString());
            context.Response.Headers.Add("Retry-After", WindowSeconds.ToString());

            await context.Response.WriteAsync("429: Rate limit exceeded. Try again in " + WindowSeconds + " seconds.");
            return; // STOP the pipeline
        }
    }

    // Request is ALLOWED or the path is not protected, continue 
    await next();
});


// --- 3. Minimal API Endpoints ---

// This endpoint is protected by the middleware check on the path segment '/protected'
app.MapGet("/protected/data", () =>
{
    return Results.Ok(new { Message = "Protected Data Accessed Successfully." });
});

// This endpoint is NOT protected by the middleware
app.MapGet("/public/status", () =>
{
    return Results.Ok(new { Message = "Public Status is OK." });
});

app.UseHttpsRedirection();
app.Run();



