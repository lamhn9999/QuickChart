using System.Collections.Concurrent;

namespace LQ.Services;

public class SparklineCancelService
{
    private readonly ConcurrentDictionary<(int, int), CancellationTokenSource> _map = new();

    public CancellationToken Register(int csvFileId, int fieldIndex)
    {
        var cts = new CancellationTokenSource();
        _map[(csvFileId, fieldIndex)] = cts;
        return cts.Token;
    }

    public void Unregister(int csvFileId, int fieldIndex)
    {
        if (_map.TryRemove((csvFileId, fieldIndex), out var cts))
            cts.Dispose();
    }

    public void Cancel(int csvFileId, int fieldIndex)
    {
        if (_map.TryRemove((csvFileId, fieldIndex), out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
