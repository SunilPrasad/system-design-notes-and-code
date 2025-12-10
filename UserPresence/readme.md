
# System Design: Building Scalable User Presence (Online/Offline) for Real-Time Chat



The "Green Dot." It is one of the most powerful UI elements in modern social applications. Whether it’s WhatsApp, Discord, or Slack, knowing **exactly** when a friend or colleague is online drives engagement and creates a sense of immediacy.

However, while building a basic chat app is a common project, maintaining accurate online/offline status for **millions of concurrent users** is a significant engineering challenge. It involves handling flaky mobile networks, server crashes, and millions of write operations per second.

In this guide, we will explore how to architect a robust, scalable presence system using **WebSockets** and **Redis**.

-----

## 1\. The High-Level Architecture

At scale, you cannot store presence data in your primary database (like PostgreSQL or MySQL). The write throughput is simply too high. If you have 10 million users sending a heartbeat every 10 seconds, that is **1 million writes per second**.

Instead, we rely on **Ephemeral Storage** (Redis) and a distributed WebSocket architecture.

### The Components

1.  **Client:** The mobile or web app.
2.  **Load Balancer:** Distributes connections across WebSocket servers.
3.  **WebSocket Gateway (Stateful):** Holds the persistent TCP connection with the user.
4.  **Presence Store (Redis):** Stores the `Online` status with a Time-To-Live (TTL).
5.  **Pub/Sub System:** Distributes status change events to friends/watchers.

### Architecture Diagram

```ascii
[ User A ]      [ User B ]
    |               ^
    | (1) Connect   | (5) Notify "A is Online"
    v               |
[ Load Balancer ]   |
    |               |
    v               |
[ WebSocket Svr 1 ] [ WebSocket Svr 2 ]
    |    ^          ^
    |    | (2) SET  | (4) SUB/PUB Event
    |    |   +      |
    v    |   TTL    |
 [ Redis Cluster (Presence Store) ]
```

-----

## 2\. Core Logic: Heartbeats and TTL

The hardest part of presence is not detecting when a user connects—it's accurately detecting when they **disconnect**, especially if they disconnect "uncleanly" (e.g., battery dies, signal lost in a tunnel, or app crash).

We solve this using a **Heartbeat mechanism combined with Redis Expiration.**

### Why Client-Side Heartbeats?

You might wonder: *Why not use the standard WebSocket Ping/Pong frames?*
While WebSocket Pings keep the TCP connection alive, we prefer **Application-Level (Client-Side) Heartbeats** for three reasons:

1.  **Browser Limitations:** The JavaScript WebSocket API does not expose Ping/Pong events. We need a manual message to know the connection is truly healthy.
2.  **Rich Data:** A custom heartbeat message allows us to send metadata (e.g., `status: "idle"` vs `status: "active"`).
3.  **Server Load:** It is cheaper for the server to react to incoming messages than to maintain a loop proactively pinging millions of users.

### The Presence Flow

#### A. User Comes Online

1.  User establishes a WebSocket connection.
2.  Server marks user as `Online` in Redis.
3.  Server publishes an event: `User A is Online`.

#### B. The Heartbeat (Maintaining Presence)

1.  **Client** sends a JSON packet `{ type: "heartbeat" }` every **10 seconds**.
2.  **Server** receives the heartbeat and extends the **TTL** (Time To Live) of the Redis key to **30 seconds**.

#### C. User Goes Offline (Clean Disconnect)

1.  User clicks "Log out" or closes the app.
2.  Server deletes the Redis key immediately.
3.  Server publishes event: `User A is Offline`.

#### D. User Goes Offline (Unclean Disconnect)

1.  User's internet cuts out.
2.  Server stops receiving heartbeats.
3.  The Redis key's **TTL expires** naturally after 30 seconds.
4.  The system treats the nonexistent key as an offline status.

-----

## 3\. Scaling Considerations

To handle millions of users, we need to optimize our data structures and routing.

### Redis Strategy

For 1-to-1 chat, a simple **Key-Value pair** is sufficient.

**Example Redis Command (User connects):**

```redis
# Set User 123 as online with a 30-second expiry
SET user:123:status "online" EX 30
```

**Example Redis Command (Heartbeat received):**

```redis
# Extend the expiry
EXPIRE user:123:status 30
```

### The "Thundering Herd" Problem

If 10 million users send a heartbeat every 10 seconds, your Redis cluster will be hammered.

  * **Optimization:** The WebSocket server can buffer heartbeats. If a user pings every 5 seconds, the server might only write to Redis every 20 seconds to refresh the TTL.
  * **Lua Scripts:** Use Redis Lua scripts to atomicize logic and reduce network round trips between the Server and Redis.

### Handling Flaky Networks (The "Grace Period")

Mobile networks are unstable. You don't want User A to flicker "Online... Offline... Online" just because they switched from WiFi to 4G.

  * **Solution:** Set the Redis TTL to be **2x to 3x** the heartbeat interval.
      * *Heartbeat:* Every 10s.
      * *Redis TTL:* 30s.
      * *Result:* The user must miss roughly 3 heartbeats before the world sees them as offline.

-----

## 4\. Implementation Example

Here is a simplified pseudocode representation of how the WebSocket server handles these events.

```javascript
// Constants
const HEARTBEAT_INTERVAL = 10000; // 10s
const PRESENCE_TTL = 30;          // 30s buffer

// 1. On Connection
socket.on('connection', async (userId) => {
    // Mark online in Redis with TTL
    await redis.set(`user:${userId}:status`, 'online', 'EX', PRESENCE_TTL);
    
    // Notify friends (Fan-out)
    notifyFriends(userId, 'online');
});

// 2. On Heartbeat (Ping from Client)
socket.on('heartbeat', async (userId) => {
    // Reset the countdown
    // Note: In production, we might throttle this to only write to Redis
    // once every 15 seconds to save resources.
    await redis.expire(`user:${userId}:status`, PRESENCE_TTL);
});

// 3. On Explicit Disconnect
socket.on('disconnect', async (userId) => {
    await redis.del(`user:${userId}:status`);
    notifyFriends(userId, 'offline');
});

// 4. Checking Status (e.g., when User B opens a chat with User A)
async function checkStatus(userId) {
    const status = await redis.get(`user:${userId}:status`);
    return status ? status : 'offline';
}
```

-----

## 5\. Metrics & Best Practices

If you are building this for production, stick to these metrics as a starting point:

| Metric | Recommended Value | Reasoning |
| :--- | :--- | :--- |
| **Heartbeat Interval** | 10–30 seconds | Balances mobile battery life with accuracy. |
| **Offline Timeout (TTL)** | Heartbeat + 10s buffer | Prevents flickering on minor packet loss. |
| **Database** | Redis (Cluster Mode) | Low latency, high write throughput, supports native TTL. |
| **Protocol** | WebSockets (WSS) | Persistent connection is required for real-time push. |

-----

## Conclusion

Implementing a heartbeat-based presence system with Redis provides:

1.  **Accuracy:** Handles both clean logouts and crashes effectively.
2.  **Scalability:** Decouples the state (Redis) from the connection handler (WebSocket Server), allowing you to add more servers horizontally.
3.  **Performance:** Ephemeral storage ensures your main database isn't bogged down by millions of status writes.

This architecture powers the "Green Dot" for the world's largest apps, ensuring that when you see "Online," you know someone is truly there.

-----

**Next Steps:**
Once you have this running, the next challenge is **Fan-Out**. When Justin Bieber goes offline, how do you notify 100 million followers without crashing the server? (Hint: You don't notify everyone). We will cover that in our next post on **"Scalable Group Chat Architecture."**