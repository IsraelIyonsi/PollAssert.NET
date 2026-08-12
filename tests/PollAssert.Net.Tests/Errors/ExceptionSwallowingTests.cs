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

        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => attempts >= 2);
        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => attempts >= 3);

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

        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => attempts >= 2);

        await task;
        Assert.Equal(2, attempts);
    }
}
