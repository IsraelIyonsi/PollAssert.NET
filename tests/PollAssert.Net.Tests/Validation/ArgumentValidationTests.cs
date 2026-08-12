using PollAssert;

namespace PollAssert.Tests.Validation;

public class ArgumentValidationTests
{
    public static IEnumerable<object[]> InvalidTimeouts() => new[]
    {
        new object[] { TimeSpan.FromMilliseconds(-1) },
        new object[] { TimeSpan.FromSeconds(-5) },
    };

    [Theory]
    [MemberData(nameof(InvalidTimeouts))]
    public void AtMost_RejectsNegativeTimeout(TimeSpan timeout)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Await.AtMost(timeout));
        Assert.Equal("timeout", exception.ParamName);
    }

    public static IEnumerable<object[]> InvalidPollIntervals() => new[]
    {
        new object[] { TimeSpan.Zero },
        new object[] { TimeSpan.FromMilliseconds(-1) },
    };

    [Theory]
    [MemberData(nameof(InvalidPollIntervals))]
    public void PollInterval_RejectsNonPositiveInterval(TimeSpan interval)
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => builder.PollInterval(interval));
        Assert.Equal("interval", exception.ParamName);
    }

    public static IEnumerable<object[]> InvalidInitialDelays() => new[]
    {
        new object[] { TimeSpan.FromMilliseconds(-1) },
        new object[] { TimeSpan.FromSeconds(-1) },
    };

    [Theory]
    [MemberData(nameof(InvalidInitialDelays))]
    public void WithInitialDelay_RejectsNegativeDelay(TimeSpan delay)
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithInitialDelay(delay));
        Assert.Equal("delay", exception.ParamName);
    }

    [Fact]
    public void WithTimeProvider_RejectsNull()
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        Assert.Throws<ArgumentNullException>(() => builder.WithTimeProvider(null!));
    }

    [Fact]
    public async Task Until_RejectsNullSynchronousPredicate()
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.Until((Func<bool>)null!));
    }

    [Fact]
    public async Task Until_RejectsNullAsynchronousPredicate()
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.Until((Func<Task<bool>>)null!));
    }

    [Fact]
    public async Task UntilValue_RejectsNullSynchronousValueProvider()
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.Until((Func<int>)null!, static _ => true));
    }

    [Fact]
    public async Task UntilValue_RejectsNullMatcherForSynchronousProvider()
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.Until(static () => 1, (Func<int, bool>)null!));
    }

    [Fact]
    public async Task UntilValue_RejectsNullAsynchronousValueProvider()
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.Until((Func<Task<int>>)null!, (Func<int, bool>)(static _ => true)));
    }

    [Fact]
    public async Task UntilValue_RejectsNullMatcherForAsynchronousProvider()
    {
        var builder = Await.AtMost(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.Until(static () => Task.FromResult(1), (Func<int, bool>)null!));
    }
}
