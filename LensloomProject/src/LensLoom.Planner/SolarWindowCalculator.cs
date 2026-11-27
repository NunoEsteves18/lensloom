using System.Globalization;

namespace LensLoom.Planner;

public sealed record GeoCoordinate(double Latitude, double Longitude);

public sealed record SolarWindow(
    DateOnly Date,
    TimeZoneInfo TimeZone,
    DateTimeOffset? Sunrise,
    DateTimeOffset? Sunset,
    DateTimeOffset? MorningGoldenHourStart,
    DateTimeOffset? MorningGoldenHourEnd,
    DateTimeOffset? EveningGoldenHourStart,
    DateTimeOffset? EveningGoldenHourEnd,
    DateTimeOffset? MorningBlueHourStart,
    DateTimeOffset? MorningBlueHourEnd,
    DateTimeOffset? EveningBlueHourStart,
    DateTimeOffset? EveningBlueHourEnd);

public sealed class SolarWindowCalculator
{
    public SolarWindow Calculate(GeoCoordinate location, DateOnly date, TimeZoneInfo? timeZone = null)
    {
        if (location.Latitude is < -90.0 or > 90.0)
        {
            throw new ArgumentOutOfRangeException(nameof(location), "Latitude must be between -90 and 90.");
        }

        if (location.Longitude is < -180.0 or > 180.0)
        {
            throw new ArgumentOutOfRangeException(nameof(location), "Longitude must be between -180 and 180.");
        }

        var tz = timeZone ?? TimeZoneInfo.Local;
        var offset = tz.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue));

        var sunriseUtc = CalculateSunEventUtc(location.Latitude, location.Longitude, date, isSunrise: true, zenithDegrees: 90.833);
        var sunsetUtc = CalculateSunEventUtc(location.Latitude, location.Longitude, date, isSunrise: false, zenithDegrees: 90.833);

        var dawnCivilUtc = CalculateSunEventUtc(location.Latitude, location.Longitude, date, isSunrise: true, zenithDegrees: 96.0);
        var duskCivilUtc = CalculateSunEventUtc(location.Latitude, location.Longitude, date, isSunrise: false, zenithDegrees: 96.0);

        // Aproximação prática para Golden Hour (~6° de elevação solar / ~84° de ângulo zenital)
        var goldenMorningEndUtc = CalculateSunEventUtc(location.Latitude, location.Longitude, date, isSunrise: true, zenithDegrees: 84.0);
        var goldenEveningStartUtc = CalculateSunEventUtc(location.Latitude, location.Longitude, date, isSunrise: false, zenithDegrees: 84.0);

        DateTimeOffset? ToLocal(DateTime? utc) =>
            utc is null ? null : new DateTimeOffset(utc.Value, TimeSpan.Zero).ToOffset(offset);

        var sunrise = ToLocal(sunriseUtc);
        var sunset = ToLocal(sunsetUtc);

        return new SolarWindow(
            Date: date,
            TimeZone: tz,
            Sunrise: sunrise,
            Sunset: sunset,
            MorningGoldenHourStart: sunrise,
            MorningGoldenHourEnd: ToLocal(goldenMorningEndUtc),
            EveningGoldenHourStart: ToLocal(goldenEveningStartUtc),
            EveningGoldenHourEnd: sunset,
            MorningBlueHourStart: ToLocal(dawnCivilUtc),
            MorningBlueHourEnd: sunrise,
            EveningBlueHourStart: sunset,
            EveningBlueHourEnd: ToLocal(duskCivilUtc));
    }

    private static DateTime? CalculateSunEventUtc(
        double latitude,
        double longitude,
        DateOnly date,
        bool isSunrise,
        double zenithDegrees)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        int dayOfYear = dateTime.DayOfYear;

        double lngHour = longitude / 15.0;
        double t = dayOfYear + ((isSunrise ? 6.0 : 18.0) - lngHour) / 24.0;

        double m = (0.9856 * t) - 3.289;
        double l = m + (1.916 * Math.Sin(ToRadians(m))) + (0.020 * Math.Sin(ToRadians(2.0 * m))) + 282.634;
        l = NormalizeDegrees(l);

        double ra = ToDegrees(Math.Atan(0.91764 * Math.Tan(ToRadians(l))));
        ra = NormalizeDegrees(ra);

        double lQuadrant = Math.Floor(l / 90.0) * 90.0;
        double raQuadrant = Math.Floor(ra / 90.0) * 90.0;
        ra = (ra + (lQuadrant - raQuadrant)) / 15.0;

        double sinDec = 0.39782 * Math.Sin(ToRadians(l));
        double cosDec = Math.Cos(Math.Asin(sinDec));

        double cosH = (Math.Cos(ToRadians(zenithDegrees)) - (sinDec * Math.Sin(ToRadians(latitude))))
                      / (cosDec * Math.Cos(ToRadians(latitude)));

        if (cosH is > 1.0 or < -1.0)
        {
            return null;
        }

        double h = isSunrise
            ? 360.0 - ToDegrees(Math.Acos(cosH))
            : ToDegrees(Math.Acos(cosH));

        h /= 15.0;

        double localMeanTime = h + ra - (0.06571 * t) - 6.622;
        double utcHour = NormalizeHours(localMeanTime - lngHour);

        int hours = (int)Math.Floor(utcHour);
        double fractionalMinutes = (utcHour - hours) * 60.0;
        int minutes = (int)Math.Floor(fractionalMinutes);
        int seconds = (int)Math.Round((fractionalMinutes - minutes) * 60.0);

        var baseUtc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
        return baseUtc.AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);
    }

    private static double ToRadians(double deg) => deg * (Math.PI / 180.0);
    private static double ToDegrees(double rad) => rad * (180.0 / Math.PI);

    private static double NormalizeDegrees(double deg)
    {
        double result = deg % 360.0;
        return result < 0 ? result + 360.0 : result;
    }

    private static double NormalizeHours(double hours)
    {
        double result = hours % 24.0;
        return result < 0 ? result + 24.0 : result;
    }

    public static string FormatWindowSummary(SolarWindow window)
    {
        string Fmt(DateTimeOffset? dt) => dt?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";

        return $"Date {window.Date:yyyy-MM-dd} ({window.TimeZone.Id}) | " +
               $"Morning Golden: {Fmt(window.MorningGoldenHourStart)}-{Fmt(window.MorningGoldenHourEnd)} | " +
               $"Evening Golden: {Fmt(window.EveningGoldenHourStart)}-{Fmt(window.EveningGoldenHourEnd)}";
    }
}