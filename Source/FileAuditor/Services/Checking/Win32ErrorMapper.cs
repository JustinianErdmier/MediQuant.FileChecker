namespace FileAuditor.Services.Checking;

/// <summary>
///     Maps file-system exceptions, by Win32 error code and by type, to check outcomes.
/// </summary>
internal static class Win32ErrorMapper
{
    private const int Win32Facility = 0x8007;

    /// <summary>
    ///     Maps an exception thrown while probing a path to an outcome.
    /// </summary>
    /// <param name="ex">The exception to map.</param>
    /// <returns>The outcome; anything unrecognised is <see cref="CheckOutcome.UnverifiedOther" />.</returns>
    public static MappedOutcome Map(Exception ex)
    {
        // A missing drive carries the same HRESULT as a missing directory (ERROR_PATH_NOT_FOUND), so the type has to be checked first.
        if (ex is DriveNotFoundException)
        {
            return new MappedOutcome(CheckOutcome.UnverifiedDriveOrShareNotFound);
        }

        // Only HRESULTs in the Win32 facility carry a Win32 code in their low word; any other HRESULT must not be misread as one
        // (that could turn an unrelated failure into "does not exist").
        int code = ((ex.HResult >> 16) & 0xFFFF) == Win32Facility ? ex.HResult & 0xFFFF : 0;

        switch (code)
        {
            case 2:
            case 3:
                return new MappedOutcome(CheckOutcome.MissingDoesNotExist);
            case 5:
                return new MappedOutcome(CheckOutcome.UnverifiedPermissionDenied);
            case 51:
            case 53:
            case 64:
            case 1222:
            case 1231:
                return new MappedOutcome(CheckOutcome.UnverifiedNetworkUnreachable);
            case 15:
            case 67:
                return new MappedOutcome(CheckOutcome.UnverifiedDriveOrShareNotFound);
            case 206:
                return new MappedOutcome(CheckOutcome.UnverifiedPathTooLong);
            case 123:
            case 161:
                return new MappedOutcome(CheckOutcome.UnverifiedInvalidCharacters);
        }

        return ex switch
        {
            FileNotFoundException or DirectoryNotFoundException => new MappedOutcome(CheckOutcome.MissingDoesNotExist),
            UnauthorizedAccessException => new MappedOutcome(CheckOutcome.UnverifiedPermissionDenied),
            PathTooLongException => new MappedOutcome(CheckOutcome.UnverifiedPathTooLong),
            ArgumentException or NotSupportedException => new MappedOutcome(CheckOutcome.UnverifiedInvalidCharacters),
            _ => new MappedOutcome(CheckOutcome.UnverifiedOther, "I/O error", ex.Message)
        };
    }
}
