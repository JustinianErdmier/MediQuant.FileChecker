namespace FileAuditor.Services.Scanning;

/// <summary>
///     Resolves a folder to the final path Windows uses for it: the target of a junction or symbolic link, the underlying folder of a <c>subst</c> drive, the UNC
///     path of a mapped drive.
/// </summary>
internal static class FinalPath
{
    private const uint FileFlagBackupSemantics = 0x02000000;

    private const uint OpenExisting = 3;

    private const uint ShareAll = 7;

    /// <summary>Gets the final path of an existing folder.</summary>
    /// <param name="path">The folder's path.</param>
    /// <returns>The final path without the <c>\\?\</c> prefix, or <see langword="null" /> when the folder cannot be opened.</returns>
    public static string? Of(string path)
    {
        try
        {
            // No access rights are asked for, only the handle's identity; opening a directory needs the backup-semantics flag, and opening follows links.
            using SafeFileHandle handle = CreateFileW(path, dwDesiredAccess: 0, ShareAll, lpSecurityAttributes: IntPtr.Zero, OpenExisting, FileFlagBackupSemantics,
                                                      hTemplateFile: IntPtr.Zero);

            if (handle.IsInvalid)
            {
                return null;
            }

            StringBuilder buffer = new(capacity: 1024);
            uint          length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, dwFlags: 0);

            if (length == 0)
            {
                return null;
            }

            if (length > buffer.Capacity)
            {
                buffer = new StringBuilder((int)length + 1);
                length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, dwFlags: 0);

                if (length == 0)
                {
                    return null;
                }
            }

            string result = buffer.ToString();

            if (result.StartsWith(value: @"\\?\UNC\", StringComparison.Ordinal))
            {
                return @"\\" + result[8..];
            }

            return result.StartsWith(value: @"\\?\", StringComparison.Ordinal) ? result[4..] : result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    [ DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true) ]
    private static extern SafeFileHandle CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
                                                     uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [ DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true) ]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle hFile, StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);
}