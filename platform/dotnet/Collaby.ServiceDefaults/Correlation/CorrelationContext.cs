using System.Diagnostics;
using System.Threading;

namespace Collaby.ServiceDefaults.Correlation;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentCorrelation = new();

    public static string? CurrentId => Normalize(CurrentCorrelation.Value);

    public static IDisposable BeginScope(string? correlationId = null)
    {
        var previous = CurrentCorrelation.Value;
        CurrentCorrelation.Value = Normalize(correlationId) ?? CreateNew();
        return new RestoreScope(previous);
    }

    public static string GetOrCreate()
    {
        var current = Normalize(CurrentCorrelation.Value);
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        current = CreateNew();
        CurrentCorrelation.Value = current;
        return current;
    }

    public static void Set(string? correlationId)
    {
        CurrentCorrelation.Value = Normalize(correlationId);
    }

    private static string? Normalize(string? correlationId)
        => string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();

    private static string CreateNew()
        => ActivityTraceId.CreateRandom().ToString();

    private sealed class RestoreScope : IDisposable
    {
        private readonly string? _previous;
        private bool _disposed;

        public RestoreScope(string? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentCorrelation.Value = _previous;
            _disposed = true;
        }
    }
}
