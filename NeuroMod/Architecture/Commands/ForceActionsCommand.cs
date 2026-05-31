using System;
using System.Diagnostics;
using System.Reflection;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using NeuroMod;

namespace NeuroMod.Architecture.Commands
{
    /// <summary>
    /// ICommand that encapsulates forcing actions for an ActionWindow.
    /// Useful for undo/redo pipelines and logging via CommandManager.
    /// </summary>
    /// <pre>The supplied window represents an ActionWindow-compatible context with the expected backing fields.</pre>
    /// <post>Executing the command transitions the window into a forced action flow when preconditions are satisfied.</post>
    public sealed class ForceActionsCommand : ICommand
    {
        private readonly object _windowObj;

        /// <summary>
        /// Initializes a new instance of the <see cref="ForceActionsCommand"/> class.
        /// </summary>
        /// <param name="window">The window object that will be used to force actions.</param>
        /// <pre><paramref name="window"/> references a non-null ActionWindow-compatible object.</pre>
        /// <post>The command stores the provided window reference for later execution.</post>
        public ForceActionsCommand(object window)
        {
            _windowObj = window ?? throw new ArgumentNullException(nameof(window));
        }

        /// <summary>
        /// Gets the command name used for diagnostics and command history.
        /// </summary>
        /// <pre>The command wraps one ActionWindow-compatible object.</pre>
        /// <post>The property returns the stable command name used by command infrastructure.</post>
        public string Name => "ForceActions";

        /// <summary>
        /// Forces actions for the wrapped action window when the window is in a valid state.
        /// </summary>
        /// <pre>The wrapped window can be cast to an ActionWindow and the websocket connection is available.</pre>
        /// <post>The action force request is sent to the API client or the method exits or throws when required preconditions are not met.</post>
        public void Execute()
        {
            // We expect the window to be of the concrete type; keep method defensive
            if (!(_windowObj is NeuroSdk.Actions.ActionWindow window))
                return;

            if (window.CurrentState != NeuroSdk.Actions.ActionWindow.State.Registered)
            {
                NeuroLogger.LogWarning($"Cannot force actions in state {window.CurrentState}", "ActionWindow", window.TraceId);
                return;
            }

            if (WebsocketConnection.Instance == null)
            {
                string error = "Cannot force actions - WebsocketConnection instance is null";
                throw new InvalidOperationException(error);
            }

            // Use the window's configured getters
            var queryGetter = window.GetType().GetField("_forceQueryGetter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(window) as Func<string>;
            var stateGetter = window.GetType().GetField("_forceStateGetter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(window) as Func<string?>;
            var ephemeralField = window.GetType().GetField("_forceEphemeralContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool ephemeral = false;
            if (ephemeralField != null && ephemeralField.GetValue(window) is bool b) ephemeral = b;

            if (queryGetter == null || stateGetter == null)
            {
                throw new InvalidOperationException("Force query or state getters are null when trying to force actions");
            }

            // Retrieve actions list
            var actionsField = window.GetType().GetField("_actions", BindingFlags.NonPublic | BindingFlags.Instance);
            var actions = actionsField?.GetValue(window) as System.Collections.Generic.IEnumerable<NeuroSdk.Actions.INeuroAction>;
            var actionsList = actions ?? System.Linq.Enumerable.Empty<NeuroSdk.Actions.INeuroAction>();

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                // update state
                var prev = window.CurrentState;
                // set state via reflection (private setter)
                var stateProp = window.GetType().GetProperty("CurrentState", BindingFlags.Public | BindingFlags.Instance);
                var setter = stateProp?.GetSetMethod(true);
                setter?.Invoke(window, new object[] { NeuroSdk.Actions.ActionWindow.State.Forced });

                string query = queryGetter();
                string? state = stateGetter();

                NeuroMod.Integration.Api.ApiClient.Send(new ActionsForce(query, state, ephemeral, actionsList));
                NeuroLogger.Log($"Forced actions via command with query: '{query}', state: '{state}', ephemeral: {ephemeral}; prevState={prev}; duration={sw.ElapsedMilliseconds}ms", "ActionWindow", window.TraceId);
            }
            catch (Exception ex)
            {
                // rethrow so CommandManager caller can observe
                throw;
            }
        }

        /// <summary>
        /// Attempts to undo the forced action request.
        /// </summary>
        /// <pre>Forced action requests are not modeled as reversible operations by default.</pre>
        /// <post>No state changes are performed because undo is intentionally unsupported.</post>
        public void Undo()
        {
            // Forcing actions is destructive; undo not supported by default.
        }
    }
}
