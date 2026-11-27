using System.Text;

namespace LensLoom.Planner;

public sealed record SessionAlert(
    string SessionTitle,
    DateTimeOffset TriggeredAt,
    int WeatherScore,
    ConditionRating Rating,
    string WindowSummary,
    string Message);

public interface ISessionAlertDispatcher
{
    Task<SessionAlert?> EvaluateAndDispatchAsync(
        string sessionTitle,
        SolarWindow window,
        WeatherEvaluation evaluation,
        int minimumScore = 65,
        string? logFilePath = null,
        CancellationToken cancellationToken = default);
}

public sealed class ConsoleSessionAlertDispatcher : ISessionAlertDispatcher
{
    public async Task<SessionAlert?> EvaluateAndDispatchAsync(
        string sessionTitle,
        SolarWindow window,
        WeatherEvaluation evaluation,
        int minimumScore = 65,
        string? logFilePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionTitle);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(evaluation);

        if (evaluation.Score < minimumScore)
        {
            return null;
        }

        var windowSummary = SolarWindowCalculator.FormatWindowSummary(window);
        var reasonsText = evaluation.Reasons.Count > 0
            ? string.Join(" ", evaluation.Reasons)
            : "Conditions look favorable.";

        var alert = new SessionAlert(
            SessionTitle: sessionTitle.Trim(),
            TriggeredAt: DateTimeOffset.Now,
            WeatherScore: evaluation.Score,
            Rating: evaluation.Rating,
            WindowSummary: windowSummary,
            Message: $"[ALERT:{evaluation.Rating}] {sessionTitle.Trim()} (Score {evaluation.Score}/100) — {windowSummary} | {reasonsText}");

        Console.WriteLine(alert.Message);

        if (!string.IsNullOrWhiteSpace(logFilePath))
        {
            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(
                logFilePath,
                $"{alert.TriggeredAt:O} | {alert.Message}{Environment.NewLine}",
                Encoding.UTF8,
                cancellationToken);
        }

        return alert;
    }
}