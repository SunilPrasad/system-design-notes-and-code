**C# Distributed Sliding Window Rate Limiter with Redis (Lua Script)**

This project demonstrates a scalable implementation of the Sliding Window Log Rate Limiter designed for distributed environments using Redis and Lua scripting.

**⚠️ Problem: Why Local Rate Limiting Fails in Distributed Systems**

ASP.NET Core’s built-in rate limiter stores counters in memory of the current process. This works perfectly only when your app runs on a single instance. But the moment your application scales to 2, 5, 50, or 100 instances, the entire logic breaks.

**Solution: Atomic Operations with Redis Lua Script**

To enforce a single, global rate limit across all servers, we use Redis as our shared store. To prevent race conditions (where two servers check the count simultaneously), we wrap the logic into a single, atomic Lua script.


The Sliding Window Log algorithm is a rate-limiting technique that measures traffic using a true time window, not fixed intervals like "per minute" or "per second."

It works by tracking the exact time of every request and counting only the requests that happened in the most recent time window.

How it Conceptually Works

1. Every time a request arrives, you record the exact timestamp of that request.

2. You also keep a list/log of recent timestamps.

3. For each new request, you look back over the last X seconds (your rate limit window).

4. You count how many timestamps fall inside that window.

5. If the count is below the allowed limit → allow the request

6. If it has reached the limit → block the request

```
local key = KEYS[1]
local limit = tonumber(ARGV[1])
local window = tonumber(ARGV[2])
local current_time_ms = tonumber(ARGV[3])

-- 1. Calculate the start of the sliding window
local trim_score = current_time_ms - (window * 1000)

-- 2. Trim: Remove all requests older than the window start time
redis.call('ZREMRANGEBYSCORE', key, 0, trim_score)

-- 3. Count: Get the current number of requests in the window
local request_count = redis.call('ZCARD', key)

-- 4. Check & Record
if request_count < limit then
    -- Allowed: Add the current request timestamp to the ZSET
    redis.call('ZADD', key, current_time_ms, current_time_ms)

    -- Set Expiration (TTL)
    redis.call('EXPIRE', key, window + 1)
    
    return 1
else
    -- Denied
    return 0
end
```
