using IntranetPrueba.Helpers;

namespace IntranetPrueba.Services.Models;

public static class AuditRetentionPolicy
{
    public const int RetentionDays = 90;

    public static DateTime GetEarliestAllowedDate(DateTime utcNow) =>
        ColombiaTime.Convert(utcNow).Date.AddDays(-RetentionDays);

    public static DateTime GetLatestAllowedDate(DateTime utcNow) =>
        ColombiaTime.Convert(utcNow).Date;

    public static DateTime GetRetentionCutoffUtc(DateTime utcNow) =>
        utcNow.AddDays(-RetentionDays);
}
