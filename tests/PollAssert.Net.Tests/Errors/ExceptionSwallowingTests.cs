using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Errors;

public class ExceptionSwallowingTests
{
    [Fact]
    public async Task Until_DefaultsToSwallowingPredicateExceptionsUntilTheyStopThrowing()
    {
        var provider = new ManualTimeProvider();
        var attempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(3))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                attempts++;
                if (attempts < 3)
                {
                    throw new InvalidOperationException("resource not ready");
                }

                return true;
            });

        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));

        await task;
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Until_PropagatesImmediatelyWhenIgnoreExceptionsIsDisabled()
    {
        var provider = new ManualTimeProvider();
        var failure = new InvalidOperationException("boom");
        var attempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .IgnoreExceptions(false)
            .Until(bool () =>
            {
                attempts++;
                throw failure;
            });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(failure, thrown);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task UntilValue_DefaultsToSwallowingMatcherExceptionsUntilTheMatcherStopsThrowing()
    {
        // Pins a deliberate design choice: the value-probing overloads evaluate `matcher(value)`
        // inside the same try/catch as the probe itself, so under the default
        // IgnoreExceptions(true) a matcher that throws is treated exactly like a probe that
        // throws (swallowed and retried) rather than propagating immediately. This mirrors
        // Awaitility's behavior for a throwing condition evaluation.
        var provider = new ManualTimeProvider();
        var matcherAttempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(3))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(static () => "ready", value =>
            {
                matcherAttempts++;
                if (matcherAttempts < 3)
                {
                    throw new InvalidOperationException("matcher not ready");
                }

                return value == "ready";
            });

        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));

        var result = await task;
        Assert.Equal("ready", result);
        Assert.Equal(3, matcherAttempts);
    }

    [Fact]
    public async Task UntilValue_MatcherExceptionPropagatesImmediatelyWhenIgnoreExceptionsIsDisabled()
    {
        var provider = new ManualTimeProvider();
        var failure = new InvalidOperationException("matcher boom");
        var matcherAttempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .IgnoreExceptions(false)
            .Until(static () => "ready", bool (_) =>
            {
                matcherAttempts++;
                throw failure;
            });

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(failure, thrown);
        Assert.Equal(1, matcherAttempts);
    }

    [Fact]
    public async Task Until_IgnoreExceptionsExplicitlyEnabled_BehavesLikeDefault()
    {
        var provider = new ManualTimeProvider();
        var attempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(2))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .IgnoreExceptions(true)
            .Until(() =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new InvalidOperationException("still warming up");
                }

                return true;
            });

        provider.Advance(TimeSpan.FromSeconds(1));

        await task;
        Assert.Equal(2, attempts);
    }
}
