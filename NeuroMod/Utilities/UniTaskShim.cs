using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// Define the missing attributes for .NET Framework 4.7.2
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Delegate | AttributeTargets.Enum)]
    internal sealed class AsyncMethodBuilderAttribute : Attribute
    {
        public Type BuilderType { get; }

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

        public UniTask(Task task)
        {
            _task = task ?? Task.CompletedTask;
        }

        public static UniTask CompletedTask => new(Task.CompletedTask);

        public static UniTask FromResult()
        {
            return new UniTask(Task.CompletedTask);
        }

        public static UniTask Delay(int millisecondsDelay)
        {
            return new UniTask(Task.Delay(millisecondsDelay));
        }

        public static UniTask Delay(TimeSpan delay)
        {
            return new UniTask(Task.Delay(delay));
        }

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

        // Awaiter support
        public TaskAwaiter GetAwaiter()
        {
            return _task.GetAwaiter();
        }

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

        public UniTask(Task<T> task)
        {
            _task = task ?? Task.FromResult(default(T)!);
        }

        public UniTask(T result)
        {
            _task = Task.FromResult(result);
        }

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

        // Awaiter support
        public TaskAwaiter<T> GetAwaiter()
        {
            return _task.GetAwaiter();
        }

        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            return _task.ConfigureAwait(continueOnCapturedContext);
        }
    }

    /// <summary>
    /// Method builder for async UniTask methods
    /// </summary>
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
    /// Method builder for async UniTask&lt;T&gt; methods
    /// </summary>
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
    /// Extension methods for UniTask
    /// </summary>
    public static class UniTaskExtensions
    {
        /// <summary>
        /// Runs a UniTask without waiting for it to complete (fire and forget)
        /// </summary>
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
        /// Runs a UniTask&lt;T&gt; without waiting for it to complete (fire and forget)
        /// </summary>
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