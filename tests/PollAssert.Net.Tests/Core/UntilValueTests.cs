using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Core;

public class UntilValueTests
{
    private const int TargetValue = 3;

    public static IEnumerable<object[]> ValueProbeModes() => new[]
    {
        new object[] { false },
        new object[] { true },
    };

    [Theory]
    [MemberData(nameof(ValueProbeModes))]
    public async Task UntilValue_ReturnsMatchedValueOnceMatcherSucceeds(bool useAsyncProvider)
    {
        var provider = new ManualTimeProvider();
        var counter = 0;

        int Probe() => ++counter;

        var builder = Await.AtMost(TimeSpan.FromSeconds(10))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider);

        var task = useAsyncProvider
            ? builder.Until(() => Task.FromResult(Probe()), static n => n == TargetValue)
            : builder.Until(Probe, static n => n == TargetValue);

        for (var i = 0; i < TargetValue - 1; i++)
        {
            var expectedCounter = i + 2;
            await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => counter >= expectedCounter);
        }

        var result = await task;
        Assert.Equal(TargetValue, result);
    }

    [Fact]
    public async Task UntilValue_ReturnsImmediatelyWhenFirstValueAlreadyMatches()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .Until(static () => "ready", static value => value == "ready");

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal("ready", await task);
    }

    [Fact]
    public async Task UntilValue_ThrowsConditionTimeoutException_WhenMatcherNeverSucceeds()
    {
        var provider = new ManualTimeProvider();
        var callCount = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(2))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(() => { callCount++; return -1; }, static n => n == TargetValue);

        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => callCount >= 2);
        await provider.AdvanceUntilAsync(TimeSpan.FromSeconds(1), () => callCount >= 3);

        await Assert.ThrowsAsync<ConditionTimeoutException>(() => task);
    }
}
