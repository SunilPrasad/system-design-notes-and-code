namespace CircuitBreakerDemo;

/// <summary>
/// Simulates an unreliable downstream service (e.g., a payment API or database).
/// You control when it's healthy vs. failing to observe circuit breaker behavior.
/// </summary>
public class FlakyService
{
    private bool _isHealthy = true;
    private readonly int _latencyMs;

    public FlakyService(int latencyMs = 50)
    {
        _latencyMs = latencyMs;
    }

    public bool IsHealthy => _isHealthy;

    public void SetHealthy(bool healthy) => _isHealthy = healthy;

    public async Task<string> CallAsync()
    {
        // Simulate network latency
        await Task.Delay(_latencyMs);

        if (!_isHealthy)
            throw new HttpRequestException("Service unavailable (simulated outage)");

        return "OK";
    }
}
