using CircuitBreakerDemo;

Console.WriteLine("==============================================");
Console.WriteLine("   CIRCUIT BREAKER PATTERN — SIMULATION");
Console.WriteLine("==============================================\n");

var settings = new CircuitBreakerSettings
{
    FailureThreshold = 3,               // Trip after 3 consecutive failures
    OpenDuration = TimeSpan.FromSeconds(5) // Stay open for 5 seconds
};

var breaker = new CircuitBreaker(settings);
var service = new FlakyService(latencyMs: 30);

// ── PHASE 1: Normal operation (service is healthy) ──────────────
Console.WriteLine("── PHASE 1: Service is HEALTHY ──");
Console.WriteLine("Sending 5 requests...\n");

for (int i = 1; i <= 5; i++)
{
    await SendRequest(breaker, service, i);
}

// ── PHASE 2: Service goes down — watch the breaker trip ─────────
Console.WriteLine("\n── PHASE 2: Service goes DOWN ──");
Console.WriteLine("Simulating outage...\n");
service.SetHealthy(false);

for (int i = 6; i <= 12; i++)
{
    await SendRequest(breaker, service, i);
}

// ── PHASE 3: Wait for the Open timeout to elapse ────────────────
Console.WriteLine("\n── PHASE 3: Waiting for Open timeout (5s)... ──\n");
await Task.Delay(TimeSpan.FromSeconds(6));

// Service is still down — HalfOpen probe will fail, circuit reopens
Console.WriteLine("Service still DOWN. Sending probe request...\n");
await SendRequest(breaker, service, 13);

// ── PHASE 4: Service recovers ───────────────────────────────────
Console.WriteLine("\n── PHASE 4: Service RECOVERS ──");
Console.WriteLine("Waiting for Open timeout again (5s)...\n");
await Task.Delay(TimeSpan.FromSeconds(6));

service.SetHealthy(true);
Console.WriteLine("Service is back UP. Sending probe request...\n");

for (int i = 14; i <= 18; i++)
{
    await SendRequest(breaker, service, i);
}

// ── Summary ─────────────────────────────────────────────────────
Console.WriteLine("\n==============================================");
Console.WriteLine("   SUMMARY");
Console.WriteLine("==============================================");
Console.WriteLine($"  Total Successes : {breaker.TotalSuccesses}");
Console.WriteLine($"  Total Failures  : {breaker.TotalFailures}");
Console.WriteLine($"  Total Rejected  : {breaker.TotalRejected} (fast-failed by circuit breaker)");
Console.WriteLine($"  Final State     : {breaker.State}");
Console.WriteLine("==============================================\n");

// ─────────────────────────────────────────────────────────────────

async Task SendRequest(CircuitBreaker cb, FlakyService svc, int requestId)
{
    try
    {
        var result = await cb.ExecuteAsync(() => svc.CallAsync());
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  Request #{requestId:D2}: SUCCESS ({result}) | State: {cb.State}");
    }
    catch (CircuitBreakerOpenException ex)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine($"  Request #{requestId:D2}: REJECTED — {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  Request #{requestId:D2}: FAILED — {ex.Message} | Failures: {cb.ConsecutiveFailures}/{settings.FailureThreshold}");
    }
    finally
    {
        Console.ResetColor();
    }
}
