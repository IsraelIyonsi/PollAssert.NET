# PollAssert.NET

Awaitility for .NET: poll until an asynchronous condition becomes true in tests, with `AtMost`, `PollInterval` and `Until`, built on `TimeProvider`. Zero dependencies.

Asynchronous systems settle on their own schedule: a background job finishes, a cache invalidates, a queue drains, an eventually-consistent read catches up. Testing that "eventually X happens" with a fixed `Task.Delay` is either too short (flaky) or too long (slow), and a hand-rolled retry loop gets rewritten slightly differently in every test file. The Java ecosystem solved this years ago with Awaitility. The .NET port of Awaitility was abandoned in 2023. PollAssert.NET is a small, focused replacement built for .NET from the ground up: a fluent poll-until-true helper with an accurate timeout, honest error messages, and a `TimeProvider` seam so the wait itself is unit-testable with a fake clock instead of a real stopwatch.

## Install

```
dotnet add package PollAssert.Net
```

## Usage

### Wait for a boolean condition

```csharp
using PollAssert;

await Await.AtMost(TimeSpan.FromSeconds(5))
    .PollInterval(TimeSpan.FromMilliseconds(100))
    .Until(() => queue.IsEmpty);
```

`Until` accepts a synchronous `Func<bool>` or an asynchronous `Func<Task<bool>>`, so it works equally well against an in-memory flag or an HTTP health check.

```csharp
await Await.AtMost(TimeSpan.FromSeconds(10))
    .Until(async () => (await httpClient.GetAsync("/health")).IsSuccessStatusCode);
```

### Wait for a value to reach a target and use it

```csharp
using PollAssert;

int finalCount = await Await.AtMost(TimeSpan.FromSeconds(3))
    .PollInterval(TimeSpan.FromMilliseconds(50))
    .Until(() => repository.CountProcessed(), n => n == 3);
```

The value-probing overload returns the value that satisfied the matcher, so a test can both wait for a condition and assert on the result it produced in one call.

### A not-yet-ready call should not fail the wait early

By default, an exception thrown while evaluating the condition is swallowed and treated as "not ready yet"; the wait keeps retrying until the timeout instead of failing on the first flaky read.

```csharp
await Await.AtMost(TimeSpan.FromSeconds(5))
    .Until(() => repository.GetLatestOrder().Status == "Shipped");
// GetLatestOrder() throwing NotFoundException on early polls does not fail the wait.
```

Turn that off when a predicate exception should fail fast instead:

```csharp
await Await.AtMost(TimeSpan.FromSeconds(5))
    .IgnoreExceptions(false)
    .Until(() => repository.GetLatestOrder().Status == "Shipped");
```

### Deterministic tests with a fake clock

`AwaitCondition` reads time exclusively through `TimeProvider` (real time by default), so a test can inject a fake clock and drive the wait without sleeping the test thread:

```csharp
using Microsoft.Extensions.Time.Testing;
using PollAssert;

var timeProvider = new FakeTimeProvider();
var task = Await.AtMost(TimeSpan.FromSeconds(10))
    .PollInterval(TimeSpan.FromSeconds(1))
    .WithTimeProvider(timeProvider)
    .Until(() => worker.IsDone);

timeProvider.Advance(TimeSpan.FromSeconds(1));
timeProvider.Advance(TimeSpan.FromSeconds(1));
worker.MarkDone();
timeProvider.Advance(TimeSpan.FromSeconds(1));

await task;
```

### Timeout failures are diagnosable

When the condition never becomes true, `Until` throws `ConditionTimeoutException` with a message stating how long it waited, how many polls ran, and the last observed value or the last exception the predicate threw:

```
Condition was not met within 5000 ms after 6 polls. Last observed value: 2
```

```
Condition was not met within 5000 ms after 3 polls. Last poll threw InvalidOperationException: connection refused
```

## API

| Member | Purpose |
|---|---|
| `Await.AtMost(TimeSpan timeout)` | Starts a wait that fails after `timeout`, including any initial delay |
| `.PollInterval(TimeSpan interval)` | Time between evaluations; defaults to 100 ms |
| `.WithInitialDelay(TimeSpan delay)` | Delay before the first evaluation; counts towards `timeout` |
| `.WithTimeProvider(TimeProvider timeProvider)` | Injects the clock; defaults to `TimeProvider.System` |
| `.IgnoreExceptions(bool ignoreExceptions = true)` | Swallow-and-retry predicate exceptions; enabled by default |
| `.Until(Func<bool>)` / `.Until(Func<Task<bool>>)` | Poll a predicate until it returns `true` |
| `.Until<T>(Func<T>, Func<T, bool>)` / `.Until<T>(Func<Task<T>>, Func<T, bool>)` | Poll a value until it matches, returning the matching value |

Correctness the library specifically guarantees:

- The poll interval is honored with a real delay between evaluations; there is no busy-spin.
- The condition is always evaluated before a timeout is declared, so the final evaluation at the timeout boundary is never skipped.
- The initial delay elapses before the first evaluation and counts towards the overall timeout, so a timeout smaller than the initial delay still guarantees exactly one evaluation before failing.
- Elapsed time is measured through `TimeProvider.GetElapsedTime`, so timeout accuracy holds under both the real clock and an injected fake one.

## Dependencies and AOT

Zero runtime dependencies. The library is built entirely on `System.Threading.Tasks` and the in-box `TimeProvider` (introduced in .NET 8), with no reflection, no dynamic code generation, and no `Type.GetType`/reflection-based dispatch anywhere in the polling path. It is trimming- and Native AOT-safe.

## License

MIT. See [LICENSE](LICENSE).
