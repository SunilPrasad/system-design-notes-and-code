using StackExchange.Redis;

public class SlidingWindowRateLimiter : IDisposable
{
    // The Lua script as a raw string
    private const string SlidingRateLimiterScript = @"
        local key = KEYS[1]
        local limit = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local current_time_ms = tonumber(ARGV[3])

        local trim_score = current_time_ms - (window * 1000)

        redis.call('ZREMRANGEBYSCORE', key, 0, trim_score)

        local request_count = redis.call('ZCARD', key)

        if request_count < limit then
            redis.call('ZADD', key, current_time_ms, current_time_ms)
            redis.call('EXPIRE', key, window + 1)
            return 1
        else
            return 0
        end
    ";

    private readonly IDatabase _db;
    private readonly ConnectionMultiplexer _redis;

    private const string RedisConnectionString = "localhost:6379,allowAdmin=true";

    public SlidingWindowRateLimiter()
    {
        _redis = ConnectionMultiplexer.Connect(RedisConnectionString);
        _db = _redis.GetDatabase();
    }

    public async Task<bool> IsRequestAllowed(string userIdentifier, int limit, int windowSeconds)
    {
        RedisKey key = $"rate_limit:{userIdentifier}";
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Evaluate the script **directly as string**
        RedisResult result = await _db.ScriptEvaluateAsync(
            SlidingRateLimiterScript,
            new RedisKey[] { key },
            new RedisValue[] { limit, windowSeconds, now }
        );

        return (long)result == 1;
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
