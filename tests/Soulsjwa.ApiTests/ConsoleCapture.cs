using System.Text;

namespace Soulsjwa.ApiTests;

/// <summary>
/// A <see cref="TextWriter"/> that collects everything written to it and can be
/// read back at any time from another thread.
///
/// <para>
/// <see cref="StringWriter"/> cannot do the second part: Serilog's Console sink
/// writes from a request-handling thread while the test thread reads, and
/// <see cref="StringBuilder.ToString"/> walks the chunk list, so an append that
/// reallocates mid-walk throws
/// <c>ArgumentOutOfRangeException (Parameter 'chunkLength')</c> — a flake that
/// only appears under load.
/// </para>
///
/// <para>
/// <see cref="Console.SetOut"/> wraps its argument in a synchronized writer, so
/// writes are already serialized against each other; what it cannot serialize
/// is a direct read of the buffer underneath. Hence the lock here, covering
/// both sides.
/// </para>
/// </summary>
public sealed class ConsoleCapture : TextWriter
{
    private readonly StringBuilder _buffer = new();
    private readonly Lock _gate = new();

    public override Encoding Encoding => Encoding.UTF8;

    // TextWriter's remaining overloads all funnel into these three.
    public override void Write(char value)
    {
        lock (_gate) _buffer.Append(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (_gate) _buffer.Append(buffer, index, count);
    }

    public override void Write(string? value)
    {
        lock (_gate) _buffer.Append(value);
    }

    public override string ToString()
    {
        lock (_gate) return _buffer.ToString();
    }
}
