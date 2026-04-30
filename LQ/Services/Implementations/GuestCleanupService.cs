using LQ.Data.Database;
using LQ.Models.TableModels;
using Microsoft.EntityFrameworkCore;

namespace LQ.Services.Implementations;

public class GuestCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GuestCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);
    // Anything still Processing past this window is an orphan from a crashed/restarted upload.
    private static readonly TimeSpan StaleProcessingAge = TimeSpan.FromMinutes(10);
    private const string StaleProcessingPayload = """{"error":"Server restarted during upload"}""";

    public GuestCleanupService(IServiceScopeFactory scopeFactory, ILogger<GuestCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LQDbContext>();

                var cutoff = DateTime.UtcNow - MaxAge;
                var deleted = await db.CSVFiles
                    .Where(f => f.GuestToken != null && f.CreatedTime < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("GuestCleanup: removed {Count} expired guest file(s).", deleted);

                var staleCutoff = DateTime.UtcNow - StaleProcessingAge;
                var staleRows = await db.CSVFiles
                    .Where(f => f.Status == UploadStatus.Processing && f.ModifiedTime < staleCutoff)
                    .ToListAsync(stoppingToken);
                foreach (var f in staleRows)
                {
                    f.Status = UploadStatus.Failed;
                    f.ProcessingPayload = StaleProcessingPayload;
                }
                if (staleRows.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("GuestCleanup: marked {Count} stale Processing upload(s) as Failed.", staleRows.Count);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "GuestCleanup: error during cleanup run.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
