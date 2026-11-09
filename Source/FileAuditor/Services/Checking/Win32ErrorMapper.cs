namespace FileAuditor.Services.Checking;

/// <summary>Maps file-system exceptions, by Win32 error code and by type, to check outcomes.</summary>
internal static class Win32ErrorMapper
{
    /// <summary>The facility code of an HRESULT that wraps a Win32 error (<c>FACILITY_WIN32</c>).</summary>
    private const int Win32Facility = 0x8007;

    /// <summary>Maps an exception thrown while probing a path to an outcome.</summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>The outcome; anything unrecognised is <see cref="CheckOutcomes.UnverifiedOther" />.</returns>
    public static MappedOutcome Map(Exception exception)
    {
        // A missing drive carries the same HRESULT as a missing directory (ERROR_PATH_NOT_FOUND), so the type has to be checked first.
        if (exception is DriveNotFoundException)
        {
            return new MappedOutcome(CheckOutcomes.UnverifiedDriveOrShareNotFound);
        }

        // Only HRESULTs in the Win32 facility carry a Win32 code in their low word; any other HRESULT must not be misread as one
        // (that could turn an unrelated failure into "does not exist").
        int code = ((exception.HResult >> 16) & 0xFFFF) is Win32Facility ? exception.HResult & 0xFFFF : 0;

        return code switch
        {
            2 or 3                         => new MappedOutcome(CheckOutcomes.MissingDoesNotExist),
            5                              => new MappedOutcome(CheckOutcomes.UnverifiedPermissionDenied),
            51 or 53 or 64 or 1222 or 1231 => new MappedOutcome(CheckOutcomes.UnverifiedNetworkUnreachable),
            15 or 67                       => new MappedOutcome(CheckOutcomes.UnverifiedDriveOrShareNotFound),
            206                            => new MappedOutcome(CheckOutcomes.UnverifiedPathTooLong),
            123 or 161                     => new MappedOutcome(CheckOutcomes.UnverifiedInvalidCharacters),
            var _ => exception switch
            {
                FileNotFoundException or DirectoryNotFoundException => new MappedOutcome(CheckOutcomes.MissingDoesNotExist),
                UnauthorizedAccessException                         => new MappedOutcome(CheckOutcomes.UnverifiedPermissionDenied),
                PathTooLongException                                => new MappedOutcome(CheckOutcomes.UnverifiedPathTooLong),
                ArgumentException or NotSupportedException          => new MappedOutcome(CheckOutcomes.UnverifiedInvalidCharacters),
                var _                                               => new MappedOutcome(CheckOutcomes.UnverifiedOther, Reason: "I/O error", exception.Message)
            }
        };
    }
}
