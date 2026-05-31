namespace NeuroMod.Architecture
{
    /// <summary>
    /// Defines a state transition contract for action-related workflows.
    /// </summary>
    /// <pre>Implementations receive a context object representing the active action window or related state carrier.</pre>
    /// <post>The context transitions into or out of the implementation-defined state.</post>
    public interface IActionState
    {
        /// <summary>
        /// Gets the display or diagnostic name of the state.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Enters the current state for the supplied context.
        /// </summary>
        /// <param name="window">The state context, intentionally typed as <see cref="object"/> to avoid tight coupling.</param>
        /// <pre><paramref name="window"/> references a context understood by the implementation.</pre>
        /// <post>The implementation has applied any setup required for the entered state.</post>
        void Enter(object window);

        /// <summary>
        /// Exits the current state for the supplied context.
        /// </summary>
        /// <param name="window">The state context, intentionally typed as <see cref="object"/> to avoid tight coupling.</param>
        /// <pre><paramref name="window"/> references a context understood by the implementation.</pre>
        /// <post>The implementation has applied any cleanup required for the exited state.</post>
        void Exit(object window);
    }
}
