namespace CircuitBreakerDemo;

public enum CircuitState
{
    Closed,   // Normal operation — requests flow through
    Open,     // Tripped — requests are rejected immediately
    HalfOpen  // Testing — allow one request to probe if service recovered
}

public class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(TimeSpan retryAfter)
        : base($"Circuit breaker is OPEN. Retry after {retryAfter.TotalSeconds:F1}s.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}

public class CircuitBreakerSettings
{
    /// <summary>Number of consecutive failures before tripping to Open.</summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>How long to stay Open before moving to HalfOpen.</summary>
    public TimeSpan OpenDuration { get; init; } = TimeSpan.FromSeconds(10);
}

public class CircuitBreaker
{
    private readonly CircuitBreakerSettings _settings;
    private readonly object _lock = new();

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTime _openedAt;
    private int _totalSuccesses;
    private int _totalFailures;
    private int _totalRejected;

    public CircuitBreaker(CircuitBreakerSettings? settings = null)
    {
        _settings = settings ?? new CircuitBreakerSettings();
    }

    public CircuitState State
    {
        get
        {
            lock (_lock)
            {
                // If we're Open and the timeout has elapsed, transition to HalfOpen
                if (_state == CircuitState.Open && DateTime.UtcNow - _openedAt >= _settings.OpenDuration)
                {
                    TransitionTo(CircuitState.HalfOpen);
                }
                return _state;
            }
        }
    }

    public int ConsecutiveFailures => _consecutiveFailures;
    public int TotalSuccesses => _totalSuccesses;
    public int TotalFailures => _totalFailures;
    public int TotalRejected => _totalRejected;

    /// <summary>
    /// Execute an action through the circuit breaker.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        EnsureCircuitAllowsRequest();

        try
        {
            var result = await action();
            OnSuccess();
            return result;
        }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    /// <summary>
    /// Execute a void action through the circuit breaker.
    /// </summary>
    public async Task ExecuteAsync(Func<Task> action)
    {
        await ExecuteAsync(async () =>
        {
            await action();
            return true; // dummy return
        });
    }

    private void EnsureCircuitAllowsRequest()
    {
        lock (_lock)
        {
            // Check if Open timeout has elapsed
            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _openedAt >= _settings.OpenDuration)
                {
                    TransitionTo(CircuitState.HalfOpen);
                    // Allow this request as the HalfOpen probe
                }
                else
                {
                    _totalRejected++;
                    var remaining = _settings.OpenDuration - (DateTime.UtcNow - _openedAt);
                    throw new CircuitBreakerOpenException(remaining);
                }
            }
            // Closed and HalfOpen both allow requests through
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _totalSuccesses++;

            if (_state == CircuitState.HalfOpen)
            {
                // Probe succeeded — service is healthy, close the circuit
                _consecutiveFailures = 0;
                TransitionTo(CircuitState.Closed);
            }
            else if (_state == CircuitState.Closed)
            {
                _consecutiveFailures = 0;
            }
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _totalFailures++;
            _consecutiveFailures++;

            if (_state == CircuitState.HalfOpen)
            {
                // Probe failed — service still unhealthy, reopen
                TransitionTo(CircuitState.Open);
            }
            else if (_state == CircuitState.Closed && _consecutiveFailures >= _settings.FailureThreshold)
            {
                TransitionTo(CircuitState.Open);
            }
        }
    }

    private void TransitionTo(CircuitState newState)
    {
        if (_state == newState) return;

        var oldState = _state;
        _state = newState;

        if (newState == CircuitState.Open)
            _openedAt = DateTime.UtcNow;

        Console.ForegroundColor = newState switch
        {
            CircuitState.Closed => ConsoleColor.Green,
            CircuitState.Open => ConsoleColor.Red,
            CircuitState.HalfOpen => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };
        Console.WriteLine($"  >> Circuit: {oldState} -> {newState}");
        Console.ResetColor();
    }
}
