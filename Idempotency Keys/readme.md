# Idempotency Keys for POST APIs

## 1. Why This Matters

`POST` endpoints usually create side effects (charge money, create order, reserve seats).
If the same request is sent twice, side effects can happen twice.

Common causes of duplicates:
- Client timeout and retry
- Mobile app retry on weak network
- API gateway retry policy
- Message redelivery
- User double-clicking a payment button

Without idempotency, a single user action can become double charge, duplicate order, or oversold inventory.

## 2. What Is an Idempotency Key?

An idempotency key is a client-provided unique token attached to a mutating request.

Rule:
- Same `Idempotency-Key` + same logical request => server returns the same result, not a second side effect.

Typical header:

```http
Idempotency-Key: 5f5a9a1b-3e4c-4c66-93b0-a4f7f8a0f9e3
```

## 3. Real-World Use Cases

- Payments: `POST /payments/charge`
- Order placement: `POST /orders`
- Wallet top-up: `POST /wallet/topup`
- Subscription create/upgrade: `POST /subscriptions`
- Ticket booking: `POST /reservations`
- Expensive external calls: tax invoice generation, shipping label creation

Finance and booking systems get the most benefit because duplicate side effects are costly.

## 4. How It Works (Basic Flow)

1. Client generates a unique key per business action.
2. Client sends request with `Idempotency-Key`.
3. Server checks idempotency store:
   - Not found: reserve key, process request.
   - Found and completed: return stored response.
   - Found and processing: return `202 Accepted` (or retry signal) to avoid duplicate processing.
4. After success/failure, server stores final response for that key.
5. Retries with same key return the same stored response.

## 5. Data Model (Recommended)

Store enough to safely replay responses and detect key misuse.

```sql
CREATE TABLE idempotency_records (
    id BIGSERIAL PRIMARY KEY,
    tenant_id        TEXT NOT NULL,
    idempotency_key  TEXT NOT NULL,
    request_hash     TEXT NOT NULL,
    status           TEXT NOT NULL, -- processing | completed | failed
    response_code    INT,
    response_body    TEXT,
    resource_type    TEXT,
    resource_id      TEXT,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at       TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX uq_idem_tenant_key
ON idempotency_records (tenant_id, idempotency_key);
```

Why these fields matter:
- `tenant_id`: prevents cross-user key collision.
- `request_hash`: blocks malicious/accidental reuse of same key with different payload.
- `status`: handles in-flight duplicates.
- `response_code` + `response_body`: deterministic replay.
- `expires_at`: cleanup policy.

## 6. Distributed Environment: Multiple Servers, One Key

### Problem
Two app instances can receive the same payment request at almost the same time.

Example:
- Request goes to Server A.
- Client times out and retries.
- Retry goes to Server B.
- Both try to charge card.

### Correct Pattern
Use a **shared idempotency store** with an atomic uniqueness guarantee.

Options:
- SQL with unique constraint (`tenant_id`, `idempotency_key`)
- Redis with atomic `SET NX` + persistent final record

For payment-grade correctness, SQL uniqueness + transaction is preferred.

### Distributed Flow (Payment Example)

1. Both Server A and Server B receive same key.
2. Both attempt `INSERT` into idempotency table.
3. Only one succeeds (winner) due to unique index.
4. Winner marks row as `processing`, executes payment.
5. Loser sees duplicate key:
   - If `processing`, return `202 Accepted` + retry hint.
   - If `completed`, return cached response immediately.
6. Winner updates row to `completed` with exact response payload.
7. Any future retry with same key gets same response, no second charge.

This prevents duplicate execution across servers.

## 7. Critical Guardrails

### 7.1 Request Hash Validation
If same key is reused with different payload, reject with `409 Conflict`.

Reason: prevents this bug:
- Request 1: key `abc`, amount `100`
- Request 2: key `abc`, amount `1000`

### 7.2 TTL / Retention
Keep records long enough to cover client retry windows.
Typical TTL:
- Payments: 24h to 72h
- Normal create APIs: 1h to 24h

### 7.3 In-Flight Timeout
If server crashes mid-processing, key can be stuck in `processing`.
Use:
- `processing` timeout + recovery job
- Or payment provider reconciliation by external transaction id

### 7.4 Propagate Key Downstream
If calling payment gateway, pass idempotency key (or derived operation id) to gateway too.
Layered idempotency is safer than relying only on your API tier.

## 8. HTTP Behavior Recommendation

- First successful execution: normal `201 Created` or `200 OK`
- Duplicate retry after completion: same `201/200` and same body
- Duplicate while processing: `202 Accepted` or `409 Conflict` with retry guidance
- Same key, different payload: `409 Conflict`

Consistency in behavior is important for client retry logic.

## 9. C#-Style Pseudocode (Service Layer)

```csharp
public async Task<IResult> CreatePaymentAsync(string tenantId, string key, PaymentRequest req)
{
    var reqHash = ComputeHash(req);

    // 1) Atomic claim (unique key)
    var claimed = await _idemRepo.TryInsertProcessingAsync(tenantId, key, reqHash);

    if (!claimed)
    {
        var existing = await _idemRepo.GetAsync(tenantId, key);

        if (existing.RequestHash != reqHash)
            return Results.Conflict("Idempotency key reused with different payload.");

        if (existing.Status == "completed")
            return Results.Content(existing.ResponseBody, "application/json", existing.ResponseCode);

        return Results.Accepted($"/payments/status/{key}");
    }

    try
    {
        var payment = await _paymentGateway.ChargeAsync(req);
        var response = new { paymentId = payment.Id, status = "succeeded" };

        await _idemRepo.MarkCompletedAsync(tenantId, key, 201, JsonSerializer.Serialize(response));
        return Results.Created($"/payments/{payment.Id}", response);
    }
    catch (Exception ex)
    {
        // Store deterministic failure response if needed
        await _idemRepo.MarkFailedAsync(tenantId, key, 500, "{\"error\":\"temporary failure\"}");
        throw;
    }
}
```

## 10. Payment-Specific Notes

For duplicate payment prevention in distributed systems, use both:
- Internal idempotency (your API)
- External provider idempotency (payment gateway)

Also store a business operation id like `merchant_order_id` and enforce uniqueness at DB level.
Even if retries bypass one layer, another layer still blocks duplicates.

## 11. Common Mistakes

- Storing key only in process memory (fails in multi-instance deployments)
- No unique index in shared DB
- No request hash validation
- Returning different payload for duplicate replay
- Very short TTL that expires before delayed retries arrive
- Treating transient failure as safe to re-execute without reconciliation

## 12. Summary

Idempotency keys make `POST` operations retry-safe.
In distributed systems, the core requirement is a **shared atomic store** with uniqueness and response replay.
For payments, combine API-level idempotency + provider-level idempotency + DB uniqueness for strong protection against duplicate charges.
