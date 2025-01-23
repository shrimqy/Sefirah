namespace Sefirah.App.Extensions;

public static class TaskExtensions
{
    public static async Task WithTimeoutAsync(this Task task, TimeSpan timeout)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout)))
        {
            await task;
        }
    }

    public static async Task<T?> WithTimeoutAsync<T>(this Task<T> task, TimeSpan timeout, T? defaultValue = default)
    {
        if (task == await Task.WhenAny(task, Task.Delay(timeout)))
            return await task;

        return defaultValue;
    }

    public static async Task<TOut> AndThen<TIn, TOut>(this Task<TIn> inputTask, Func<TIn, Task<TOut>> mapping)
    {
        var input = await inputTask;

        return await mapping(input);
    }

    public static void FireAndForget(this Task task)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Debug.WriteLine($"Fire and forget task failed: {t.Exception}");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
