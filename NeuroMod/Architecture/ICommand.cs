namespace NeuroMod.Architecture
{
    /// <summary>
    /// Defines a command that can be executed and optionally undone.
    /// </summary>
    /// <pre>Implementations encapsulate a discrete action and expose a stable command name.</pre>
    /// <post>Command managers can invoke the command and coordinate undo and redo behavior.</post>
    public interface ICommand
    {
        /// <summary>
        /// Gets the unique or descriptive name of the command.
        /// </summary>
        /// <pre>The command implementation exposes a stable identifier for diagnostics or history.</pre>
        /// <post>The property returns the implementation-defined command name.</post>
        string Name { get; }

        /// <summary>
        /// Executes the command operation.
        /// </summary>
        /// <pre>Any required command state has already been captured by the implementation.</pre>
        /// <post>The command's forward behavior has been applied.</post>
        void Execute();

        /// <summary>
        /// Reverts the command operation when supported.
        /// </summary>
        /// <pre>The command has either been executed previously or handles unsupported undo scenarios safely.</pre>
        /// <post>The command's state is reverted or intentionally left unchanged when undo is not supported.</post>
        void Undo();
    }
}
