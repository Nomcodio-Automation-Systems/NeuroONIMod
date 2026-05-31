namespace NeuroMod.Architecture.Plugins
{
    /// <summary>
    /// Defines a plugin strategy that can apply action-related behavior to a supplied context.
    /// </summary>
    /// <pre>Implementations expose a stable key and know how to operate on their expected context type.</pre>
    /// <post>Consumers can select and invoke strategies without depending on concrete implementations.</post>
    public interface IActionStrategy
    {
        /// <summary>
        /// Gets the unique key used to identify the strategy.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Applies the strategy to the supplied context.
        /// </summary>
        /// <param name="context">The context object understood by the strategy implementation.</param>
        /// <pre><paramref name="context"/> references a strategy-compatible context object.</pre>
        /// <post>The strategy-specific behavior has been applied to the supplied context.</post>
        void Apply(object context);
    }
}
