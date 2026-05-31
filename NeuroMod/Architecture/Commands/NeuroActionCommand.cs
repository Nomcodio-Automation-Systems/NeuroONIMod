using System;
using Cysharp.Threading.Tasks;
using NeuroSdk.Actions;

namespace NeuroMod.Architecture.Commands
{
    /// <summary>
    /// ICommand wrapper for an `INeuroAction` that triggers the action's async execution.
    /// Execution is fire-and-forget via UniTask to avoid blocking the caller.
    /// </summary>
    /// <pre>An <see cref="INeuroAction"/> instance and optional execution data are available when the command is constructed.</pre>
    /// <post>The action is triggered asynchronously during execution and generic undo remains unsupported.</post>
    public sealed class NeuroActionCommand : ICommand
    {
        private readonly INeuroAction _action;
        private readonly object? _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="NeuroActionCommand"/> class.
        /// </summary>
        /// <param name="action">The Neuro action to execute.</param>
        /// <param name="data">Optional action input data.</param>
        /// <pre><paramref name="action"/> references a valid action implementation.</pre>
        /// <post>The command stores the action and optional payload for later execution.</post>
        public NeuroActionCommand(INeuroAction action, object? data = null)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _data = data;
        }

        /// <summary>
        /// Gets the command name derived from the wrapped action.
        /// </summary>
        /// <pre>The command wraps a non-null Neuro action implementation.</pre>
        /// <post>The property returns the wrapped action name for diagnostics and command history.</post>
        public string Name => _action.Name;

        /// <summary>
        /// Executes the wrapped Neuro action in a fire-and-forget manner.
        /// </summary>
        /// <pre>The wrapped action instance is available and can accept the stored optional payload.</pre>
        /// <post>The action has been scheduled for asynchronous execution without blocking the caller.</post>
        public void Execute()
        {
            try
            {
                // Run the async execution without awaiting (preserve existing flow)
                _action.ExecuteAsync(_data).Forget();
            }
            catch (Exception)
            {
                // Swallow to preserve command manager stability; logging can be added by caller
            }
        }

        /// <summary>
        /// Attempts to undo the wrapped action.
        /// </summary>
        /// <pre>Generic Neuro actions do not expose a universal undo contract.</pre>
        /// <post>No state changes are performed because undo is not implemented for the wrapped action.</post>
        public void Undo()
        {
            // Undo not implemented for generic Neuro actions
        }
    }
}
