#nullable enable

namespace NeuroSdk.Utilities;

/// <summary>
/// Wraps a format template so strongly typed string constants can delay formatting until use.
/// </summary>
/// <pre>The wrapped template is a valid <see cref="string.Format(string, object[])"/> format string.</pre>
/// <post>Formatting uses the original template without mutating the wrapper instance.</post>
internal sealed class FormatString
{
    private readonly string _str;

    private FormatString(string str)
    {
        _str = str;
    }

    /// <summary>
    /// Formats the wrapped template with the supplied arguments.
    /// </summary>
    /// <param name="args">The arguments to inject into the wrapped format template.</param>
    /// <returns>The formatted string.</returns>
    /// <pre><paramref name="args"/> match the placeholders expected by the wrapped format string.</pre>
    /// <post>Returns the formatted string produced by the stored template and the provided arguments.</post>
    public string Format(params object[] args)
    {
        return string.Format(_str, args);
    }

    /// <summary>
    /// Converts a raw format string into a <see cref="FormatString"/> wrapper.
    /// </summary>
    /// <param name="str">The format template to wrap.</param>
    /// <pre><paramref name="str"/> is a non-null format template literal.</pre>
    /// <post>The returned wrapper preserves the original format string for later formatting.</post>
    public static implicit operator FormatString(string str)
    {
        return new FormatString(str);
    }
}