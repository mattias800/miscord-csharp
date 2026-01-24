using System.Reactive.Linq;

namespace Snacka.Client.Tests;

/// <summary>
/// Test helper extensions for working with observables in tests.
/// </summary>
public static class TestExtensions
{
    /// <summary>
    /// Gets the first value from an observable with a timeout.
    /// Prevents tests from hanging indefinitely.
    /// </summary>
    public static T FirstWithTimeout<T>(this IObservable<T> observable, TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromSeconds(5);
        return observable
            .Timeout(actualTimeout)
            .FirstAsync()
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Gets the first value from an observable synchronously with a default 5 second timeout.
    /// </summary>
    public static T WaitFirst<T>(this IObservable<T> observable)
    {
        return observable.FirstWithTimeout(TimeSpan.FromSeconds(5));
    }
}
