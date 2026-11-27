using LensLoom.Core.Models;

namespace LensLoom.Core.Abstractions;

public interface ISunTimesProvider
{
    Task<SunTimes> GetSunTimesAsync(
        GeoLocation location,
        DateOnly date,
        CancellationToken cancellationToken = default);
}

public interface IWeatherForecastProvider
{
    Task<IReadOnlyList<WeatherForecast>> GetHourlyForecastAsync(
        GeoLocation location,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}

public interface IWindowScorer
{
    ShootingWindow Evaluate(
        GeoLocation location,
        string windowLabel,
        DateTimeOffset start,
        DateTimeOffset end,
        WeatherForecast forecast);
}

public interface IAlertDispatcher
{
    Task DispatchAsync(
        IReadOnlyList<ShootingWindow> recommendedWindows,
        CancellationToken cancellationToken = default);
}