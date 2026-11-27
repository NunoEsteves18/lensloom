namespace LensLoom.Planner;

public enum ShootingStyle
{
    GoldenHour,
    Landscape,
    Street,
    LongExposure
}

public enum ConditionRating
{
    Poor,
    Fair,
    Good,
    Excellent
}

public sealed record WeatherSnapshot(
    DateTimeOffset Timestamp,
    double CloudCoverPercent,
    double VisibilityKm,
    double WindSpeedKmh,
    double PrecipitationMmPerHour,
    double TemperatureC);

public sealed record WeatherEvaluation(
    int Score,
    ConditionRating Rating,
    IReadOnlyList<string> Reasons);

public sealed class WeatherConditionsEvaluator
{
    public WeatherEvaluation Evaluate(WeatherSnapshot weather, ShootingStyle style = ShootingStyle.GoldenHour)
    {
        ArgumentNullException.ThrowIfNull(weather);

        double score = 100.0;
        var reasons = new List<string>();

        if (weather.PrecipitationMmPerHour > 0.2)
        {
            double rainPenalty = Math.Min(45.0, weather.PrecipitationMmPerHour * 10.0);
            score -= rainPenalty;
            reasons.Add($"Precipitation ({weather.PrecipitationMmPerHour:0.#} mm/h) reduces comfort/clarity.");
        }

        if (weather.VisibilityKm < 5.0)
        {
            double visPenalty = Math.Min(35.0, (5.0 - weather.VisibilityKm) * 6.0);
            score -= visPenalty;
            reasons.Add($"Low visibility ({weather.VisibilityKm:0.#} km).");
        }

        if (weather.WindSpeedKmh > 25.0)
        {
            double windPenalty = Math.Min(30.0, (weather.WindSpeedKmh - 25.0) * 0.8);
            score -= windPenalty;
            reasons.Add($"Strong wind ({weather.WindSpeedKmh:0.#} km/h) may affect tripods/stability.");
        }

        switch (style)
        {
            case ShootingStyle.GoldenHour:
                if (weather.CloudCoverPercent is < 15.0 or > 85.0)
                {
                    score -= 15.0;
                    reasons.Add($"Cloud cover ({weather.CloudCoverPercent:0.#}%) is outside ideal 15%-85% golden-hour range.");
                }
                else
                {
                    reasons.Add($"Cloud cover ({weather.CloudCoverPercent:0.#}%) supports warm sky diffusion.");
                }
                break;

            case ShootingStyle.LongExposure:
                if (weather.WindSpeedKmh > 18.0)
                {
                    score -= 15.0;
                    reasons.Add("Wind above 18 km/h risks motion blur on long exposures.");
                }
                break;

            case ShootingStyle.Landscape:
                if (weather.CloudCoverPercent > 90.0)
                {
                    score -= 10.0;
                    reasons.Add("Heavy overcast (>90%) flattens landscape contrast.");
                }
                break;

            case ShootingStyle.Street:
                if (weather.PrecipitationMmPerHour > 2.0)
                {
                    score -= 10.0;
                    reasons.Add("Heavy rain slows street movement.");
                }
                break;
        }

        int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);
        var rating = finalScore switch
        {
            >= 80 => ConditionRating.Excellent,
            >= 60 => ConditionRating.Good,
            >= 40 => ConditionRating.Fair,
            _ => ConditionRating.Poor
        };

        return new WeatherEvaluation(finalScore, rating, reasons);
    }
}