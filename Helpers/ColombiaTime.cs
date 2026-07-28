namespace IntranetPrueba.Helpers;

public static class ColombiaTime
{
    private static readonly TimeZoneInfo Zone = GetZone();

    private static TimeZoneInfo GetZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("America/Bogota", out var tz))
            return tz;
        return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
    }

    public static DateTime Convert(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    public static DateTime? Convert(DateTime? utc) =>
        utc.HasValue ? Convert(utc.Value) : null;

    public static DateTime ConvertToUtc(DateTime colombiaDateTime) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(colombiaDateTime, DateTimeKind.Unspecified),
            Zone);
}
