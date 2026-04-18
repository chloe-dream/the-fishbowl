namespace Fishbowl.Core.Plugins;

/// <summary>
/// A cron-scheduled background job. The host's scheduler owns the timer;
/// the job owns the work.
/// </summary>
public interface IScheduledJob
{
    string Name { get; }

    /// <summary>Standard 5-field cron expression (minute hour day month day-of-week).</summary>
    string CronExpression { get; }

    Task ExecuteAsync(CancellationToken ct);
}
