namespace FileAuditor.Services.Dialogues;

/// <summary>
///     Draws the user's attention to the main window without taking the keyboard: a Windows sound and, when the window is not the active one, a flashing taskbar button. Both calls
///     are best effort; a failure is ignored.
/// </summary>
public static class WindowAttention
{
    /// <summary>The <c>MessageBeep</c> sound for information (<c>MB_ICONASTERISK</c>).</summary>
    private const uint SoundInformation = 0x40;

    /// <summary>The <c>MessageBeep</c> sound for an error (<c>MB_ICONHAND</c>).</summary>
    private const uint SoundError = 0x10;

    /// <summary>The <c>MessageBeep</c> sound for a warning (<c>MB_ICONEXCLAMATION</c>).</summary>
    private const uint SoundWarning = 0x30;

    /// <summary>The <c>MessageBeep</c> sound for a question (<c>MB_ICONQUESTION</c>).</summary>
    private const uint SoundQuestion = 0x20;

    /// <summary>Flashes both the caption and the taskbar button (<c>FLASHW_ALL</c>).</summary>
    private const uint FlashAll = 0x3;

    /// <summary>Keeps flashing until the window comes to the foreground (<c>FLASHW_TIMERNOFG</c>).</summary>
    private const uint FlashUntilForeground = 0xC;

    /// <summary>
    ///     Plays the sound for an icon and, when the window is not active, flashes its taskbar button until it comes to the foreground. The window is never activated.
    /// </summary>
    /// <param name="window">The window to flash.</param>
    /// <param name="icon">The icon of the dialogue the signal accompanies; it chooses the sound.</param>
    public static void Request(Window window, DialogueIcons icon)
    {
        try
        {
            MessageBeep(icon switch
                        {
                            DialogueIcons.Information => SoundInformation,
                            DialogueIcons.Error       => SoundError,
                            DialogueIcons.Warning     => SoundWarning,
                            var _                     => SoundQuestion
                        });

            IntPtr handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;

            if (window.IsActive || handle == IntPtr.Zero)
            {
                return;
            }

            FlashInfo info = new()
            {
                Size    = (uint) Marshal.SizeOf<FlashInfo>(),
                Window  = handle,
                Flags   = FlashAll | FlashUntilForeground,
                Count   = uint.MaxValue,
                Timeout = 0
            };

            FlashWindowEx(ref info);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { }
    }

    /// <summary>Calls the Win32 <c>MessageBeep</c> function, which plays a system sound.</summary>
    /// <param name="type">The sound to play.</param>
    /// <returns>Whether the call succeeded.</returns>
    [ DllImport(dllName: "user32.dll") ]
    private static extern bool MessageBeep(uint type);

    /// <summary>Calls the Win32 <c>FlashWindowEx</c> function, which flashes a window's caption and taskbar button.</summary>
    /// <param name="info">What to flash and how.</param>
    /// <returns>Whether the window was active before the call.</returns>
    [ DllImport(dllName: "user32.dll") ]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    /// <summary>The Win32 <c>FLASHWINFO</c> structure.</summary>
    [ StructLayout(LayoutKind.Sequential) ]
    private struct FlashInfo
    {
        /// <summary>The size of the structure in bytes.</summary>
        public uint Size;

        /// <summary>The handle of the window to flash.</summary>
        public IntPtr Window;

        /// <summary>The flash flags.</summary>
        public uint Flags;

        /// <summary>The number of times to flash.</summary>
        public uint Count;

        /// <summary>The flash rate in milliseconds; zero uses the system's cursor blink rate.</summary>
        public uint Timeout;
    }
}
