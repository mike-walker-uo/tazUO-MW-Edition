using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers;

public static class MainThreadQueue
{
    private const int MAX_ACTIONS_PER_FRAME = 256;
    private const int MAX_PROCESSING_MS = 2;

    public static ConcurrentQueue<Action> QueuedActions { get; } = new();
    private static int _mainThreadId;

    /// <summary>
    /// This will not wait for the action to complete.
    /// </summary>
    /// <param name="action"></param>
    public static void EnqueueAction(Action action)
    {
        QueuedActions.Enqueue(action);
    }

    /// <summary>
    /// This will wait for the returned result.
    /// </summary>
    /// <param name="func"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T InvokeOnMainThread<T>(Func<T> func)
    {
        if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _mainThreadId))
        {
            return func();
        }

        using var resultEvent = new ManualResetEventSlim(false);
        T result = default;
        Exception error = null;

        void action()
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                resultEvent.Set();
            }
        }

        QueuedActions.Enqueue(action);
        resultEvent.Wait();

        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        return result;
    }

    /// <summary>
    /// This will not wait for the returned result.
    /// </summary>
    /// <param name="action"></param>
    public static void InvokeOnMainThread(Action action)
    {
        QueuedActions.Enqueue(action);
    }

    public static Task InvokeOnMainThreadAsync(Action action)
    {
        if (Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _mainThreadId))
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        QueuedActions.Enqueue(() =>
        {
            try
            {
                action();
                completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    public static void ProcessQueue()
    {
        Volatile.Write(ref _mainThreadId, Thread.CurrentThread.ManagedThreadId);
        long deadline = Stopwatch.GetTimestamp()
            + Stopwatch.Frequency * MAX_PROCESSING_MS / 1000;
        int processed = 0;

        while (processed < MAX_ACTIONS_PER_FRAME
            && QueuedActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error($"Main-thread action failed: {ex}");
            }

            processed++;
            if (Stopwatch.GetTimestamp() >= deadline)
            {
                break;
            }
        }
    }

    public static void Reset()
    {
        while (QueuedActions.TryDequeue(out _)) { }
    }
}
