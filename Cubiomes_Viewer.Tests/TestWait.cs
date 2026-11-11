namespace Cubiomes_Viewer.Tests;

internal static class TestWait
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Lets queued work run until the condition holds, and fails the test when it does not in time.</summary>
    public static async Task UntilAsync(Func<bool> condition, string failureMessage)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, failureMessage);
            await Task.Delay(_pollInterval, TestContext.Current.CancellationToken);
        }
    }
}
