namespace SEBMS.Api.Services;

/// <summary>
/// Runs the recurring-transaction processor shortly after start-up (catching up on anything missed while the API
/// was off) and then hourly.
/// </summary>
public class RecurringTransactionWorker(IServiceScopeFactory scopes, ILogger<RecurringTransactionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<RecurringTransactionProcessor>();
                var created = await processor.ProcessAsync(DateOnly.FromDateTime(DateTime.Now), notify: true, ct: stoppingToken);
                if (created > 0) logger.LogInformation("Recurring transactions: created {Count}.", created);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Recurring transaction run failed; will retry at the next interval.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
