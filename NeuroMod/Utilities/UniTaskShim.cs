using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// Define the missing attributes for .NET Framework 4.7.2
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Provides the async method builder attribute missing from the target .NET Framework runtime.
    /// </summary>
    /// <pre><paramref name="builderType"/> identifies the method builder that should back the annotated async type.</pre>
    /// <post>The attribute exposes the selected builder type through <see cref="BuilderType"/>.</post>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Delegate | AttributeTargets.Enum)]
    internal sealed class AsyncMethodBuilderAttribute : Attribute
    {
        public Type BuilderType { get; }

        /// <summary>
        /// Initializes the attribute with the supplied async method builder type.
        /// </summary>
        /// <param name="builderType">The builder type associated with the annotated async type.</param>
        /// <pre><paramref name="builderType"/> is a valid async method builder implementation.</pre>
        /// <post><see cref="BuilderType"/> returns the supplied builder type.</post>
        public AsyncMethodBuilderAttribute(Type builderType)
        {
            BuilderType = builderType;
        }
    }
}

namespace Cysharp.Threading.Tasks
{
    /// <summary>
    /// .NET Framework 4.7.2 compatible UniTask implementation
    /// Provides compatibility shim for VedalAI.NeuroSdk.Unity classes
    /// while avoiding runtime dependency on actual UniTask package
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    [AsyncMethodBuilder(typeof(UniTaskMethodBuilder))]
    public readonly struct UniTask
    {
        private readonly Task _task;

        /// <summary>
        /// Wraps a <see cref="Task"/> in the compatibility UniTask representation.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        /// <pre><paramref name="task"/> may be null when callers want the shim to fall back to a completed task.</pre>
        /// <post>The wrapped task is never null internally.</post>
        public UniTask(Task task)
        {
            _task = task ?? Task.CompletedTask;
        }

        public static UniTask CompletedTask => new(Task.CompletedTask);

        /// <summary>
        /// Creates a completed non-generic UniTask.
        /// </summary>
        /// <pre>No asynchronous work needs to be represented.</pre>
        /// <post>The returned UniTask is already completed.</post>
        public static UniTask FromResult()
        {
            return new UniTask(Task.CompletedTask);
        }

        /// <summary>
        /// Creates a delayed UniTask for the specified number of milliseconds.
        /// </summary>
        /// <param name="millisecondsDelay">The delay duration in milliseconds.</param>
        /// <pre><paramref name="millisecondsDelay"/> is a valid value for <see cref="Task.Delay(int)"/>.</pre>
        /// <post>The returned UniTask completes after the requested delay.</post>
        public static UniTask Delay(int millisecondsDelay)
        {
            return new UniTask(Task.Delay(millisecondsDelay));
        }

        /// <summary>
        /// Creates a delayed UniTask for the specified duration.
        /// </summary>
        /// <param name="delay">The delay duration.</param>
        /// <pre><paramref name="delay"/> is a valid value for <see cref="Task.Delay(TimeSpan)"/>.</pre>
        /// <post>The returned UniTask completes after the requested delay.</post>
        public static UniTask Delay(TimeSpan delay)
        {
            return new UniTask(Task.Delay(delay));
        }

        /// <summary>
        /// Creates a minimal asynchronous yield point.
        /// </summary>
        /// <pre>Callers accept the shim's coarse yield approximation for .NET Framework compatibility.</pre>
        /// <post>The returned UniTask completes asynchronously on a later scheduler tick.</post>
        public static UniTask Yield()
        {
            return new UniTask(Task.Delay(1));
        }

        // Implicit conversion operators
        public static implicit operator UniTask(Task task)
        {
            return new UniTask(task);
        }

        public static implicit operator Task(UniTask unitask)
        {
            return unitask._task;
        }

        /// <summary>
        /// Gets the awaiter for the wrapped task.
        /// </summary>
        /// <pre>The UniTask instance wraps a non-null task.</pre>
        /// <post>The returned awaiter delegates directly to the wrapped task.</post>
        public TaskAwaiter GetAwaiter()
        {
            return _task.GetAwaiter();
        }

        /// <summary>
        /// Configures how continuations are scheduled after awaiting the wrapped task.
        /// </summary>
        /// <param name="continueOnCapturedContext">Whether to resume on the captured synchronization context.</param>
        /// <pre>The UniTask instance wraps a non-null task.</pre>
        /// <post>The returned awaitable forwards configuration to the wrapped task.</post>
        public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        {
            return _task.ConfigureAwait(continueOnCapturedContext);
        }
    }

    /// <summary>
    /// Generic UniTask implementation for .NET Framework 4.7.2
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    [AsyncMethodBuilder(typeof(UniTaskMethodBuilder<>))]
    public readonly struct UniTask<T>
    {
        private readonly Task<T> _task;

        /// <summary>
        /// Wraps a <see cref="Task{TResult}"/> in the compatibility UniTask representation.
        /// </summary>
        /// <param name="task">The task to wrap.</param>
        /// <pre><paramref name="task"/> may be null when callers want the shim to fall back to a completed default-value task.</pre>
        /// <post>The wrapped task is never null internally.</post>
        public UniTask(Task<T> task)
        {
            _task = task ?? Task.FromResult(default(T)!);
        }

        /// <summary>
        /// Wraps an already computed result in the compatibility UniTask representation.
        /// </summary>
        /// <param name="result">The completed result value.</param>
        /// <pre><paramref name="result"/> may be the default value for <typeparamref name="T"/>.</pre>
        /// <post>The wrapped task is already completed with <paramref name="result"/>.</post>
        public UniTask(T result)
        {
            _task = Task.FromResult(result);
        }

        /// <summary>
        /// Creates a completed UniTask containing the supplied result.
        /// </summary>
        /// <param name="result">The completed result value.</param>
        /// <pre><paramref name="result"/> may be the default value for <typeparamref name="T"/>.</pre>
        /// <post>The returned UniTask is already completed with <paramref name="result"/>.</post>
        public static UniTask<T> FromResult(T result)
        {
            return new UniTask<T>(Task.FromResult(result));
        }

        // Implicit conversion operators
        public static implicit operator UniTask<T>(Task<T> task)
        {
            return new UniTask<T>(task);
        }

        public static implicit operator Task<T>(UniTask<T> unitask)
        {
            return unitask._task;
        }

        public static implicit operator UniTask<T>(T result)
        {
            return new UniTask<T>(Task.FromResult(result));
        }

        /// <summary>
        /// Gets the awaiter for the wrapped task.
        /// </summary>
        /// <pre>The UniTask instance wraps a non-null task.</pre>
        /// <post>The returned awaiter delegates directly to the wrapped task.</post>
        public TaskAwaiter<T> GetAwaiter()
        {
            return _task.GetAwaiter();
        }

        /// <summary>
        /// Configures how continuations are scheduled after awaiting the wrapped task.
        /// </summary>
        /// <param name="continueOnCapturedContext">Whether to resume on the captured synchronization context.</param>
        /// <pre>The UniTask instance wraps a non-null task.</pre>
        /// <post>The returned awaitable forwards configuration to the wrapped task.</post>
        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            return _task.ConfigureAwait(continueOnCapturedContext);
        }
    }

    /// <summary>
    /// Method builder for async UniTask methods.
    /// </summary>
    /// <pre>The builder is used only by compiler-generated async state machines targeting the shimmed UniTask type.</pre>
    /// <post>The exposed <see cref="Task"/> represents the completion or failure of the associated async method.</post>
    public struct UniTaskMethodBuilder
    {
        private TaskCompletionSource<object> _tcs;

        public static UniTaskMethodBuilder Create()
        {
            return new UniTaskMethodBuilder();
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        { }

        public void SetResult()
        {
            if (_tcs != null)
            {
                _tcs.SetResult(new object());
            }
        }

        public void SetException(Exception exception)
        {
            if (_tcs == null)
            {
                _tcs = new TaskCompletionSource<object>();
            }

            _tcs.SetException(exception);
        }

        public UniTask Task => _tcs == null
                    ? new UniTask(System.Threading.Tasks.Task.CompletedTask)
                    : new UniTask(_tcs.Task.ContinueWith(t => { }, TaskContinuationOptions.ExecuteSynchronously));

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (_tcs == null)
            {
                _tcs = new TaskCompletionSource<object>();
            }

            awaiter.OnCompleted(stateMachine.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (_tcs == null)
            {
                _tcs = new TaskCompletionSource<object>();
            }

            awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
        }
    }

    /// <summary>
    /// Method builder for async UniTask&lt;T&gt; methods.
    /// </summary>
    /// <pre>The builder is used only by compiler-generated async state machines targeting the shimmed UniTask&lt;T&gt; type.</pre>
    /// <post>The exposed <see cref="Task"/> represents the completion or failure of the associated async method.</post>
    public struct UniTaskMethodBuilder<T>
    {
        private TaskCompletionSource<T> _tcs;

        public static UniTaskMethodBuilder<T> Create()
        {
            return new UniTaskMethodBuilder<T>();
        }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        { }

        public void SetResult(T result)
        {
            if (_tcs != null)
            {
                _tcs.SetResult(result);
            }
        }

        public void SetException(Exception exception)
        {
            if (_tcs == null)
            {
                _tcs = new TaskCompletionSource<T>();
            }

            _tcs.SetException(exception);
        }

        public UniTask<T> Task => _tcs == null ? new UniTask<T>(System.Threading.Tasks.Task.FromResult(default(T)!)) : new UniTask<T>(_tcs.Task);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (_tcs == null)
            {
                _tcs = new TaskCompletionSource<T>();
            }

            awaiter.OnCompleted(stateMachine.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (_tcs == null)
            {
                _tcs = new TaskCompletionSource<T>();
            }

            awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
        }
    }

    /// <summary>
    /// Provides fire-and-forget helpers for the UniTask compatibility shim.
    /// </summary>
    /// <pre>The caller intentionally does not await the supplied UniTask.</pre>
    /// <post>Faulted background tasks are observed and written to the console instead of being silently ignored.</post>
    public static class UniTaskExtensions
    {
        /// <summary>
        /// Runs a UniTask without waiting for it to complete.
        /// </summary>
        /// <param name="unitask">The task to observe in fire-and-forget mode.</param>
        /// <pre><paramref name="unitask"/> represents background work whose completion the caller does not need to await.</pre>
        /// <post>Any faulted exception is observed and written to the console.</post>
        public static void Forget(this UniTask unitask)
        {
            Task task = unitask;
            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    // Log or handle exceptions as needed
                    Console.WriteLine($"UniTask exception: {t.Exception}");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Runs a UniTask&lt;T&gt; without waiting for it to complete.
        /// </summary>
        /// <param name="unitask">The task to observe in fire-and-forget mode.</param>
        /// <typeparam name="T">The result type carried by the background operation.</typeparam>
        /// <pre><paramref name="unitask"/> represents background work whose completion the caller does not need to await.</pre>
        /// <post>Any faulted exception is observed and written to the console.</post>
        public static void Forget<T>(this UniTask<T> unitask)
        {
            Task<T> task = unitask;
            task.ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    // Log or handle exceptions as needed
                    Console.WriteLine($"UniTask<T> exception: {t.Exception}");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}