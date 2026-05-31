using System.Collections.Generic;

namespace NeuroMod.Architecture
{
    /// <summary>
    /// Provides a minimal command manager for command execution and undo or redo history.
    /// </summary>
    /// <pre>Commands supplied to the manager implement <see cref="ICommand"/> and capture the state they need.</pre>
    /// <post>Executed commands are recorded for undo and redo traversal.</post>
    public sealed class CommandManager
    {
        private static readonly CommandManager _instance = new();

        /// <summary>
        /// Gets the singleton command manager instance.
        /// </summary>
        /// <pre>No input is required.</pre>
        /// <post>The returned value is the shared command manager instance.</post>
        public static CommandManager Instance => _instance;

        private readonly Stack<ICommand> _undoStack = new();
        private readonly Stack<ICommand> _redoStack = new();

        private CommandManager() { }

        /// <summary>
        /// Executes a command and records it for undo.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        /// <pre><paramref name="command"/> references a valid command instance.</pre>
        /// <post>The command has been executed, added to the undo stack, and the redo stack has been cleared.</post>
        public void Execute(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
        }

        /// <summary>
        /// Gets a value indicating whether an undo operation is currently available.
        /// </summary>
        /// <pre>No input is required.</pre>
        /// <post>The returned value reflects whether the undo stack currently contains at least one command.</post>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets a value indicating whether a redo operation is currently available.
        /// </summary>
        /// <pre>No input is required.</pre>
        /// <post>The returned value reflects whether the redo stack currently contains at least one command.</post>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Undoes the most recently executed command when available.
        /// </summary>
        /// <pre>The undo stack may contain a previously executed command.</pre>
        /// <post>The most recent command is moved to the redo stack after its undo logic runs.</post>
        public void Undo()
        {
            if (!CanUndo) return;
            var cmd = _undoStack.Pop();
            cmd.Undo();
            _redoStack.Push(cmd);
        }

        /// <summary>
        /// Re-executes the most recently undone command when available.
        /// </summary>
        /// <pre>The redo stack may contain a previously undone command.</pre>
        /// <post>The most recent redo candidate is re-executed and returned to the undo stack.</post>
        public void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Push(cmd);
        }
    }
}
