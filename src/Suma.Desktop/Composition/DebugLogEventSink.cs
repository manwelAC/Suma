using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Suma.Desktop.Composition;

internal sealed class DebugLogEventSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();
        Debug.WriteLine($"[{logEvent.Timestamp:O} {logEvent.Level}] {message}");

        if (logEvent.Exception is not null)
        {
            Debug.WriteLine(logEvent.Exception);
        }
    }
}
