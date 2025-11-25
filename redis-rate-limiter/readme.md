C# Distributed Sliding Window Rate Limiter with Redis (Lua Script)

This project demonstrates a scalable implementation of the Sliding Window Log Rate Limiter designed for distributed environments using Redis and Lua scripting.

⚠️ Problem: Why Local Rate Limiting Fails in Distributed Systems

ASP.NET Core’s built-in rate limiter stores counters in memory of the current process. This works perfectly only when your app runs on a single instance. But the moment your application scales to 2, 5, 50, or 100 instances, the entire logic breaks.

Solution: Atomic Operations with Redis Lua Script

To enforce a single, global rate limit across all servers, we use Redis as our shared store. To prevent race conditions (where two servers check the count simultaneously), we wrap the logic into a single, atomic Lua script.


The Sliding Window Log algorithm is a rate-limiting technique that measures traffic using a true time window, not fixed intervals like "per minute" or "per second."

It works by tracking the exact time of every request and counting only the requests that happened in the most recent time window.

How it Conceptually Works

Every time a request arrives, you record the exact timestamp of that request.

You also keep a list/log of recent timestamps.

For each new request, you look back over the last X seconds (your rate limit window).

You count how many timestamps fall inside that window.

If the count is below the allowed limit → allow the request
If it has reached the limit → block the request

