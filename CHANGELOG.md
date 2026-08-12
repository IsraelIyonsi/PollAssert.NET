# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-08-12

### Added

- `Await.AtMost(TimeSpan)` fluent entry point returning an `AwaitCondition` builder with `PollInterval`, `WithInitialDelay`, `WithTimeProvider`, and `IgnoreExceptions`.
- `Until(Func<bool>)` and `Until(Func<Task<bool>>)` to poll a synchronous or asynchronous predicate until it returns `true`.
- `Until<T>(Func<T>, Func<T, bool>)` and `Until<T>(Func<Task<T>>, Func<T, bool>)` to poll a value provider until the produced value satisfies a matcher, returning the matching value.
- `ConditionTimeoutException`, thrown on timeout with a message stating the elapsed wait time, the number of polls, and the last observed value or the last exception the predicate threw.
- Predicate exceptions are swallowed and retried by default, so a not-yet-ready call does not fail the wait early; configurable via `IgnoreExceptions(false)` to fail fast instead.
- Built entirely on the in-box `TimeProvider` (real time by default), so waits are drivable deterministically in tests with a fake clock and require no wall-clock sleeping.
- Zero runtime dependencies; trimming- and Native AOT-safe.
