using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Core;

public class UntilBooleanTests
{
    private const int ExpectedSuccessfulPollCount = 4;

    public static IEnumerable<object[]> BooleanPredicateModes() => new[]
    {
        new object[] { false },
        new object[] { true },
    };

    [Theory]
    [MemberData(nameof(BooleanPredicateModes))]
    public async Task Until_ReturnsAsSoonAsConditionBecomesTrue(bool useAsyncPredicate)
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        bool Probe()
        {
            callCount++;
            return callCount == ExpectedSuccessfulPollCount;
        }

        var builder = Await.AtMost(TimeSpan.FromSeconds(10))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider);

        var task = useAsyncPredicate
            ? builder.Until(() => Task.FromResult(Probe()))
            : builder.Until(Probe);

        for (var i = 0; i < ExpectedSuccessfulPollCount - 1; i++)
        {
            var expectedCallCount = i + 2;
            await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => callCount >= expectedCallCount);
        }

        await task;
        Assert.Equal(ExpectedSuccessfulPollCount, callCount);
    }

    [Fact]
    public void Until_CompletesSynchronouslyWhenConditionIsAlreadyTrue()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .Until(static () => true);

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Until_AsyncPredicate_CompletesSynchronouslyWhenAlreadyTrue()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .Until(static () => Task.FromResult(true));

        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }

    [Fact]
    public async Task Until_ThrowsConditionTimeoutException_WhenConditionNeverBecomesTrue()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(3))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(() => { callCount++; return false; });

        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => callCount >= 2);
        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => callCount >= 3);
        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => callCount >= 4);

        await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
    }
}
