using PollAssert.Tests.TestSupport;
using PollAssert;

namespace PollAssert.Tests.Core;

public class UntilAssertedTests
{
    private const int ExpectedSuccessfulAttemptCount = 4;
    private const string AssertionFailureMessage = "counter has not reached the target yet";

    [Fact]
    public async Task Until_Action_ReturnsWithoutThrowing_WhenAssertionFailsThenPasses()
    {
        var provider = new ManualTimeProvider();
        var attempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(10))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(() =>
            {
                attempts++;
                if (attempts < ExpectedSuccessfulAttemptCount)
                {
                    throw new InvalidOperationException(AssertionFailureMessage);
                }
            });

        for (var i = 0; i < ExpectedSuccessfulAttemptCount - 1; i++)
        {
            provider.Advance(TimeSpan.FromSeconds(1));
        }

        await task;
        Assert.Equal(ExpectedSuccessfulAttemptCount, attempts);
    }

    [Fact]
    public async Task Until_Action_RethrowsLastAssertionException_WhenAssertionAlwaysFails()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(3))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(void () => throw new InvalidOperationException(AssertionFailureMessage));

        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal(AssertionFailureMessage, thrown.Message);
    }

    [Fact]
    public async Task Until_Action_DoesNotThrowConditionTimeoutException_WhenAssertionAlwaysFails()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(2))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(void () => throw new InvalidOperationException(AssertionFailureMessage));

        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));

        var thrown = await CaptureAsync(task);
        Assert.IsType<InvalidOperationException>(thrown);
        Assert.IsNotType<ConditionTimeoutException>(thrown);
    }

    [Fact]
    public async Task Until_Action_PreservesOriginalExceptionIdentity()
    {
        var provider = new ManualTimeProvider();
        var failure = new InvalidOperationException("original assertion failure");

        var task = Await.AtMost(TimeSpan.FromSeconds(2))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(void () => throw failure);

        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task Until_Action_PreservesOriginalStackTrace()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(2))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(ThrowFromNamedHelper);

        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains(nameof(ThrowFromNamedHelper), thrown.StackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void Until_Action_CompletesSynchronouslyWhenAssertionPassesImmediately()
    {
        var provider = new ManualTimeProvider();

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .Until(static () => { });

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Until_AsyncAssertion_ReturnsWithoutThrowing_WhenAssertionFailsThenPasses()
    {
        var provider = new ManualTimeProvider();
        var attempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(10))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(async () =>
            {
                await Task.CompletedTask;
                attempts++;
                if (attempts < ExpectedSuccessfulAttemptCount)
                {
                    throw new InvalidOperationException(AssertionFailureMessage);
                }
            });

        for (var i = 0; i < ExpectedSuccessfulAttemptCount - 1; i++)
        {
            provider.Advance(TimeSpan.FromSeconds(1));
        }

        await task;
        Assert.Equal(ExpectedSuccessfulAttemptCount, attempts);
    }

    [Fact]
    public async Task Until_AsyncAssertion_RethrowsLastAssertionException_WhenAssertionAlwaysFails()
    {
        var provider = new ManualTimeProvider();
        var failure = new InvalidOperationException("async assertion failure");

        var task = Await.AtMost(TimeSpan.FromSeconds(3))
            .PollInterval(TimeSpan.FromSeconds(1))
            .WithTimeProvider(provider)
            .Until(async () =>
            {
                await Task.CompletedTask;
                throw failure;
            });

        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));
        provider.Advance(TimeSpan.FromSeconds(1));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Same(failure, thrown);
        Assert.Equal(failure.Message, thrown.Message);
    }

    [Fact]
    public async Task Until_AsyncAssertion_CompletesWhenAssertionPassesImmediately()
    {
        var provider = new ManualTimeProvider();
        var attempts = 0;

        var task = Await.AtMost(TimeSpan.FromSeconds(5))
            .WithTimeProvider(provider)
            .Until(async () =>
            {
                await Task.CompletedTask;
                attempts++;
            });

        await task;
        Assert.Equal(1, attempts);
    }

    private static void ThrowFromNamedHelper() =>
        throw new InvalidOperationException("failure raised from a named helper");

    private static async Task<Exception> CaptureAsync(Task task)
    {
        try
        {
            await task;
            throw new InvalidOperationException("The task was expected to fault but completed successfully.");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
