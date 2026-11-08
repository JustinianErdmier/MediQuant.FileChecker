namespace FileAuditor.ViewModels.Groups;

/// <summary>Formats the durations the Audit groups show, Elapsed and Remaining, so that both tabs and both boxes read alike.</summary>
internal static class TimeText
{
    /// <summary>Ten thousand hours in seconds: far beyond any real run, small enough that no conversion can overflow.</summary>
    internal const double MaxSeconds = 36_000_000;

    /// <summary>Formats a duration as <c>hh:mm:ss</c> (hours may exceed 24) without going through <see cref="TimeSpan" />, so any value is safe.</summary>
    /// <param name="seconds">The duration in seconds; negative and non-finite values are clamped.</param>
    /// <returns>The duration as text.</returns>
    internal static string Format(double seconds)
    {
        long whole = double.IsNaN(seconds) ? 0 : (long)Math.Clamp(seconds, min: 0, MaxSeconds);

        return $"{whole / 3600:00}:{whole / 60 % 60:00}:{whole % 60:00}";
    }
}
