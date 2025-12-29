# Architecture Pattern: Asynchronous Background Processing

## 1. Problem Statement

In web applications, certain business operations are time-consuming or resource-intensive. A common example is **Bulk Notifications**: sending emails to multiple users after a status change.

If these operations are performed synchronously within the HTTP Request lifecycle:

* **Poor UX:** The user interface "freezes" or shows a spinner while waiting for the server to finish sending emails.
* **Timeouts:** If the operation takes longer than the configured HTTP timeout (e.g., 30s), the request fails, potentially leaving data in an inconsistent state.
* **Scalability:** Thread pool threads are blocked waiting for external I/O (SMTP servers), reducing the server's ability to handle new requests.

## 2. Solution Overview

To resolve this, we implement a **Producer-Consumer** pattern using `System.Threading.Channels` and ASP.NET Core `BackgroundService`.

This creates a "Fire-and-Forget" mechanism where the heavy lifting is offloaded to a background process, allowing the HTTP request to complete immediately.

### High-Level Architecture

1. **Producer (API/Controller):** Validates the request, updates the database, pushes a job ID or data payload into an **In-Memory Queue**, and returns `200 OK` immediately.
2. **The Queue (Channel):** A thread-safe buffer that holds pending jobs. It handles backpressure (preventing memory overflows) if tasks are added faster than they can be processed.
3. **Consumer (Background Worker):** A dedicated background thread that monitors the queue. It wakes up when data arrives, processes the job (e.g., sends emails), and sleeps when the queue is empty.

---

## 3. Technical Implementation

We use a strongly-typed queue to ensure type safety. For this implementation, we define a specific queue for Task Notifications.

### 3.1 The Contract (Interface)

Located in: `Domain/Contracts`

This interface allows the Application Layer to queue work without knowing the implementation details of the background runner.

```csharp
public interface ITaskNotificationQueue
{
    /// <summary>
    /// Adds a batch of task updates to the queue.
    /// If the queue is full, this operation will wait asynchronously (Non-Blocking Backpressure).
    /// </summary>
    ValueTask QueueTaskUpdatesAsync(List<ScheduleTaskUpdate> updates);

    /// <summary>
    /// Reads a batch from the queue. Used only by the Background Worker.
    /// </summary>
    ValueTask<List<ScheduleTaskUpdate>> DequeueAsync(CancellationToken cancellationToken);
}

```

### 3.2 The Infrastructure (Channel Implementation)

Located in: `Infrastructure/BackgroundWorker`

We use `System.Threading.Channels` for high-performance, thread-safe queuing.

```csharp
public class TaskNotificationQueue : ITaskNotificationQueue
{
    private readonly Channel<List<ScheduleTaskUpdate>> _queue;

    public TaskNotificationQueue()
    {
        // Configuration:
        // Capacity: 1000 items. 
        // Behavior: Wait asynchronously if full (prevents OutOfMemoryException).
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // Optimization: We have one worker reading.
            SingleWriter = false // Optimization: Multiple users writing.
        };

        _queue = Channel.CreateBounded<List<ScheduleTaskUpdate>>(options);
    }

    public async ValueTask QueueTaskUpdatesAsync(List<ScheduleTaskUpdate> updates)
    {
        if (updates == null || updates.Count == 0) return;
        await _queue.Writer.WriteAsync(updates);
    }

    public async ValueTask<List<ScheduleTaskUpdate>> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

```

### 3.3 The Consumer (Background Service)

Located in: `WebUI/BackgroundWorker`

This service runs for the lifetime of the application. It listens to the queue and executes the business logic.

**Key Design Pattern:** Since `BackgroundService` is a Singleton and the Database Context is Scoped, we must manually create a `IServiceScope` for each batch of work.

```csharp
public class EmailWorker : BackgroundService
{
    private readonly ITaskNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailWorker> _logger;

    public EmailWorker(
        ITaskNotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Email Worker Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Await Signal: This line pauses execution until data arrives (0 CPU usage).
                var batch = await _queue.DequeueAsync(stoppingToken);

                // 2. Process: Execute logic in isolation.
                await ProcessBatchAsync(batch);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown.
                break;
            }
            catch (Exception ex)
            {
                // Global Error Handler: Prevents the worker from crashing on bad data.
                _logger.LogError(ex, "Error processing background task.");
            }
        }
    }

    private async Task ProcessBatchAsync(List<ScheduleTaskUpdate> batch)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var emailService = scope.ServiceProvider.GetRequiredService<ITaskNotifyService>();
            await emailService.NotifyTaskAssignmentChangesAsync(batch);
        }
    }
}

```

---

## 4. Usage Example

### Scenario: Bulk Task Update

When a user reassigns a list of tasks, we need to notify the new assignees.

**Controller Code:**

```csharp
[HttpPost("update-tasks")]
public async Task<IActionResult> UpdateTasks([FromBody] List<TaskUpdateDto> request)
{
    // 1. Critical Path: Update Database (Synchronous/Awaited)
    // We await this because we must ensure data integrity before notifying.
    await _repository.UpdateTasksAsync(request);

    // 2. Background Path: Queue Notifications (Fire-and-Forget)
    // Map DTO to Domain Model
    var notificationPayload = request.Select(r => new ScheduleTaskUpdate { ... }).ToList();
    
    // This call returns almost instantly, merely writing to memory.
    await _queue.QueueTaskUpdatesAsync(notificationPayload);

    // 3. Response: User gets immediate feedback.
    return Ok(new { message = "Tasks updated. Notifications are being sent in the background." });
}

```

---

## 5. Best Practices & Limitations

1. **Scope Management:** Never inject a generic Scoped service (like `DbContext`) directly into the `BackgroundService` constructor. Always use `IServiceScopeFactory` to create a fresh scope inside the loop.
2. **Exception Handling:** The `ExecuteAsync` loop must never crash. Always wrap the processing logic in a `try/catch` block.
3. **Volatility:** This is an **In-Memory** queue. If the server crashes or restarts (IIS Recycle), pending items in the queue are lost.
* *Recommendation:* For mission-critical data (e.g., Financial Transactions), use a persistent queue like RabbitMQ or Hangfire. For Email Notifications, this in-memory approach is usually acceptable.