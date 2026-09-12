using OMM.Shared.Database;
using Xunit;

namespace OMM.Shared.Tests.Database;

public class DatabaseAvailabilityTests
{
    [Fact]
    public void InitialState_IsUnavailable()
    {
        var availability = new DatabaseAvailability();

        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public void MarkAvailable_WhenUnavailable_ReturnsTrueAndUpdatesState()
    {
        var availability = new DatabaseAvailability();

        var transitioned = availability.MarkAvailable();

        Assert.True(transitioned);
        Assert.True(availability.IsAvailable);
    }

    [Fact]
    public void MarkAvailable_WhenAlreadyAvailable_ReturnsFalse()
    {
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();

        var secondCall = availability.MarkAvailable();

        Assert.False(secondCall);
        Assert.True(availability.IsAvailable);
    }

    [Fact]
    public void MarkUnavailable_WhenAvailable_ReturnsTrueAndUpdatesState()
    {
        var availability = new DatabaseAvailability();
        availability.MarkAvailable();

        var transitioned = availability.MarkUnavailable();

        Assert.True(transitioned);
        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public void MarkUnavailable_WhenAlreadyUnavailable_ReturnsFalse()
    {
        var availability = new DatabaseAvailability();

        var transitioned = availability.MarkUnavailable();

        Assert.False(transitioned);
        Assert.False(availability.IsAvailable);
    }

    [Fact]
    public async Task ConcurrentMarkAvailable_ProducesExactlyOneSuccessfulTransition()
    {
        const int iterations = 100;

        for (var run = 0; run < 10; run++)
        {
            var availability = new DatabaseAvailability();
            var successCount = 0;

            var tasks = Enumerable.Range(0, iterations).Select(_ => Task.Run(() =>
            {
                if (availability.MarkAvailable())
                {
                    Interlocked.Increment(ref successCount);
                }
            }));

            await Task.WhenAll(tasks);

            Assert.Equal(1, successCount);
            Assert.True(availability.IsAvailable);
        }
    }

    [Fact]
    public async Task ConcurrentMarkUnavailable_ProducesExactlyOneSuccessfulTransition()
    {
        const int iterations = 100;

        for (var run = 0; run < 10; run++)
        {
            var availability = new DatabaseAvailability();
            availability.MarkAvailable();
            var successCount = 0;

            var tasks = Enumerable.Range(0, iterations).Select(_ => Task.Run(() =>
            {
                if (availability.MarkUnavailable())
                {
                    Interlocked.Increment(ref successCount);
                }
            }));

            await Task.WhenAll(tasks);

            Assert.Equal(1, successCount);
            Assert.False(availability.IsAvailable);
        }
    }
}
