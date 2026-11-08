namespace FileAuditor.Models.Auditing;

/// <summary>The single outcome every audited path lands in (SDD §6.3). The Unverified members are declared in the order the report lists them.</summary>
public enum CheckOutcome
{
    /// <summary>The file exists (and, when empty files are reported, has a length greater than zero).</summary>
    Found,

    /// <summary>Empty files are reported and the file exists with a length of zero.</summary>
    MissingEmpty,

    /// <summary>The path's root exists, but the file or a directory below the root does not.</summary>
    MissingDoesNotExist,

    /// <summary>The path's root (a drive letter, or <c>\\server\share</c>) does not exist for this user.</summary>
    UnverifiedDriveOrShareNotFound,

    /// <summary>Access to the file or its location is denied.</summary>
    UnverifiedPermissionDenied,

    /// <summary>The server or share in a UNC path cannot be reached.</summary>
    UnverifiedNetworkUnreachable,

    /// <summary>The path exceeds what the system can handle.</summary>
    UnverifiedPathTooLong,

    /// <summary>The unindexed audit met a junction or symbolic link (a folder or a file) and did not follow or check it.</summary>
    UnverifiedLinkNotFollowed,
    /// <summary>The path is malformed or contains illegal characters.</summary>
    UnverifiedInvalidCharacters,

    /// <summary>The path value in the index table is <c>NULL</c>, empty or whitespace.</summary>
    UnverifiedPathEmpty,

    /// <summary>Any other I/O failure; the reason and system message are recorded.</summary>
    UnverifiedOther
}
