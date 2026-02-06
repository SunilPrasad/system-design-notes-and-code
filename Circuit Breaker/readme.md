# Circuit Breaker Pattern

## The Problem

In a microservices architecture, **Service A** calls **Service B** over the network. What happens when Service B goes down?

```
Without Circuit Breaker:
────────────────────────
Service A  ──req──>  Service B (DOWN)
           ──req──>  ... timeout (30s)
           ──req──>  ... timeout (30s)
           ──req──>  ... timeout (30s)

Result: Service A hangs, threads pile up, cascading failure
```

Every request waits for a **timeout** (often 30 seconds). Meanwhile threads pile up, memory grows, and Service A itself becomes unresponsive. This is a **cascading failure** — one unhealthy service takes down the entire system.

## The Solution: Circuit Breaker

Inspired by electrical circuit breakers that trip to prevent house fires. The pattern wraps outgoing calls and monitors failures. When failures cross a threshold, it **stops making calls entirely** and fails fast.

```
With Circuit Breaker:
─────────────────────
Service A  ──req──>  Service B (DOWN)    ← failure 1
           ──req──>  Service B (DOWN)    ← failure 2
           ──req──>  Service B (DOWN)    ← failure 3 → THRESHOLD HIT

  >> Circuit: Closed -> Open

Service A  ──req──>  [CIRCUIT BREAKER]   ← rejected instantly (0ms)
           ──req──>  [CIRCUIT BREAKER]   ← rejected instantly (0ms)

Result: Service A stays responsive, returns errors fast
```

## Three States

```
                    ┌──────────────────────────────────────┐
                    │                                      │
                    ▼                                      │
             ┌───────────┐   failure threshold    ┌───────────┐
             │           │ ─────────────────────> │           │
     ───────>│  CLOSED   │                        │   OPEN    │
             │ (normal)  │                        │ (tripped) │
             │           │ <───────────────┐      │           │
             └───────────┘                 │      └─────┬─────┘
                                           │            │
                                    probe  │            │  timeout
                                   success │            │  expires
                                           │            │
                                      ┌────┴──────┐     │
                                      │           │ <───┘
                                      │ HALF-OPEN │
                                      │  (probe)  │
                                      │           │ ──── probe fails ──> back to OPEN
                                      └───────────┘
```

| State | Behavior |
|-------|----------|
| **Closed** | Requests flow normally. Failures are counted. If consecutive failures hit the threshold, transition to **Open**. |
| **Open** | All requests are **rejected immediately** (fail-fast, no network call). After a timeout period, transition to **Half-Open**. |
| **Half-Open** | Allow **one probe request** through. If it succeeds → **Closed**. If it fails → back to **Open**. |

## Why It Matters

| Without Circuit Breaker | With Circuit Breaker |
|------------------------|---------------------|
| Threads hang waiting for timeouts | Fails fast in ~0ms |
| Memory/thread pool exhaustion | Resources stay available |
| Cascading failures across services | Failure is contained |
| Slow degradation, hard to diagnose | Clear state transitions, easy to monitor |
| Keeps hammering a dead service | Gives the service time to recover |

## Key Configuration

| Parameter | What it controls | Typical value |
|-----------|-----------------|---------------|
| **Failure Threshold** | How many consecutive failures before tripping | 3–5 |
| **Open Duration** | How long to stay Open before probing | 10–60 seconds |

## Real-World Usage

- **Netflix Hystrix** — The library that popularized this pattern. Netflix has thousands of microservices; without circuit breakers, a single failure would cascade.
- **Polly (.NET)** — The standard resilience library for .NET. Supports circuit breaker, retry, timeout, bulkhead, and more.
- **AWS** — Circuit breakers in API Gateway and App Mesh.
- **Kubernetes** — Istio service mesh implements circuit breaking at the infrastructure level.

## Common Combinations

Circuit breakers are usually combined with other resilience patterns:

```
Request
  │
  ├──> Retry (with exponential backoff + jitter)
  │      │
  │      └──> Circuit Breaker
  │             │
  │             └──> Timeout
  │                    │
  │                    └──> Actual HTTP call
  │
  └──> Fallback (cached response, default value, or graceful error)
```

- **Retry**: Retries transient failures (network blip). But retries into a dead service make things worse — so put a circuit breaker around it.
- **Timeout**: Prevents a single slow call from hanging forever.
- **Fallback**: Returns a cached or default response when the circuit is open.

## Running the Demo

```bash
cd CircuitBreakerDemo
dotnet run
```

### What the simulation does

The demo simulates 4 phases to show the full circuit breaker lifecycle:

| Phase | What happens | What to observe |
|-------|-------------|-----------------|
| **1. Healthy** | 5 requests, service is up | All succeed, circuit stays **Closed** |
| **2. Outage** | Service goes down, 7 requests sent | First 3 fail → circuit trips to **Open** → remaining requests rejected instantly |
| **3. Probe fails** | Wait 5s, send 1 request (service still down) | Circuit moves to **Half-Open**, probe fails → back to **Open** |
| **4. Recovery** | Wait 5s, service comes back, 5 requests | Probe succeeds → circuit **Closes** → all requests succeed |

### Expected Output

```
── PHASE 1: Service is HEALTHY ──
  Request #01: SUCCESS (OK)
  Request #02: SUCCESS (OK)
  ...

── PHASE 2: Service goes DOWN ──
  Request #06: FAILED — Service unavailable | Failures: 1/3
  Request #07: FAILED — Service unavailable | Failures: 2/3
  Request #08: FAILED — Service unavailable | Failures: 3/3
  >> Circuit: Closed -> Open
  Request #09: REJECTED — Circuit breaker is OPEN
  Request #10: REJECTED — Circuit breaker is OPEN
  ...

── PHASE 3: Waiting for Open timeout (5s)... ──
  >> Circuit: Open -> HalfOpen
  Request #13: FAILED — probe failed
  >> Circuit: HalfOpen -> Open

── PHASE 4: Service RECOVERS ──
  >> Circuit: Open -> HalfOpen
  Request #14: SUCCESS — probe passed!
  >> Circuit: HalfOpen -> Closed
  Request #15: SUCCESS (OK)
  ...
```

## Project Structure

```
circuit breaker/
├── readme.md                              ← You are here
├── .gitignore
└── CircuitBreakerDemo/
    ├── CircuitBreakerDemo.csproj
    ├── CircuitBreaker.cs                  ← The pattern implementation
    ├── FlakyService.cs                    ← Simulated unreliable service
    └── Program.cs                         ← Simulation runner
```
