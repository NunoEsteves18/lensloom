
namespace LensLoom.Core.Models;

public sealed record GeoLocation(
    string Name,
    double Latitude,
    double Longitude);

public sealed record SunTimes(
    DateOnly Date,
    DateTimeOffset Sunrise,
    DateTimeOffset Sunset,
    DateTimeOffset MorningGoldenHourStart,
    DateTimeOffset MorningGoldenHourEnd,
    DateTimeOffset EveningGoldenHourStart,
    DateTimeOffset EveningGoldenHourEnd,
    DateTimeOffset EveningBlueHourEnd);

public sealed record WeatherForecast(
    DateTimeOffset Timestamp,
    double CloudCoverPercent,
    double WindSpeedKmh,
    double PrecipitationProbabilityPercent,
    double TemperatureCelsius);

public sealed record ShootingWindow(
    GeoLocation Location,
    string WindowLabel,
    DateTimeOffset Start,
    DateTimeOffset End,
    WeatherForecast Forecast,
    double Score,
    bool IsRecommended);