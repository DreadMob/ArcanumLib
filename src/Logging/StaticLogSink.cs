using System;
using System.Collections.Concurrent;

namespace ArcanumLib.Logging;

/// <summary>
/// Static log sink that buffers messages until a real logger is registered,
/// then forwards them. Used by core-layer disposal/error paths where the
/// Vintage Story API logger may not yet be available.
/// </summary>
public static class StaticLogSink
{
    private const int MaxBufferedMessages = 100;

    private static readonly ConcurrentQueue<string> _buffer = new();
    private static Action<string>? _logger;

    /// <summary>
    /// Writes <paramref name="message" /> to the registered logger, or buffers it
    /// if no logger has been set yet. When the buffer is full and no logger is
    /// registered, falls back to <see cref="Console.Error" /> so the message is
    /// not silently lost.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Log(string message)
    {
        if (message == null) return;

        var logger = _logger;
        if (logger != null)
        {
            logger(message);
            return;
        }

        if (_buffer.Count < MaxBufferedMessages)
        {
            _buffer.Enqueue(message);
        }
        else
        {
            // Buffer is full and no real logger — fall back to stderr so the
            // message is not silently dropped.
            Console.Error.WriteLine(message);
        }
    }

    /// <summary>
    /// Registers the real logger that <see cref="Log" /> should forward to.
    /// Any previously buffered messages are flushed to <paramref name="logger" />
    /// before subsequent messages are forwarded directly.
    /// Pass <c>null</c> to clear the registered logger.
    /// </summary>
    /// <param name="logger">The action to receive log messages, or <c>null</c> to clear.</param>
    public static void SetLogger(Action<string>? logger)
    {
        _logger = logger;

        if (logger == null) return;

        while (_buffer.TryDequeue(out var buffered))
        {
            try
            {
                logger(buffered);
            }
            catch
            {
                // If the real logger throws, stop flushing and leave remaining
                // messages in the buffer rather than crashing the caller.
                _buffer.Enqueue(buffered);
                break;
            }
        }
    }
}
