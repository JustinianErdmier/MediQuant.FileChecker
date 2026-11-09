namespace FileAuditor.Startup;

/// <summary>
///     Catches exceptions nothing else handled. One the app can survive is shown in an error dialogue and the app carries on; one it cannot survive is explained in a message box
///     before the process exits. Nothing is written to disk.
/// </summary>
public static class UnexpectedErrorHandler
{
    /// <summary>The message box style that shows an OK button and the error icon (<c>MB_OK | MB_ICONERROR</c>).</summary>
    private const uint MessageBoxOkWithErrorIcon = 0x10;

    /// <summary>The message box style that brings the box to the foreground (<c>MB_SETFOREGROUND</c>).</summary>
    private const uint MessageBoxSetForeground = 0x10000;

    /// <summary>The message box style that keeps the box above other windows (<c>MB_TOPMOST</c>).</summary>
    private const uint MessageBoxTopmost = 0x40000;

    /// <summary>The caption of the plain message box.</summary>
    private const string Title = "File Auditor";

    /// <summary>The desktop lifetime whose main window owns the notices; <see langword="null" /> until <see cref="Install" /> runs.</summary>
    private static IClassicDesktopStyleApplicationLifetime? _desktop;

    /// <summary>Set to 1 by the first fatal report, so a second fatal error on another thread waits instead of showing a second message.</summary>
    private static int _fatalReportStarted;

    /// <summary>Whether <see cref="Install" /> has already hooked the exception events.</summary>
    private static bool _isInstalled;

    /// <summary>Whether a notice is already open, so a burst of errors does not stack up dialogues. Touched only on the UI thread, so it needs no lock.</summary>
    private static bool _noticeIsOpen;

    /// <summary>Starts watching for unexpected exceptions on the UI thread, in unobserved tasks and anywhere else in the process.</summary>
    /// <param name="desktop">The desktop lifetime, whose main window owns the notices.</param>
    public static void Install(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_isInstalled)
        {
            return;
        }

        _isInstalled = true;

        _desktop = desktop;

        Dispatcher.UIThread.UnhandledException     += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException      += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    /// <summary>Shows an exception the app can survive in an error dialogue, unless a notice is already open, and lets the app carry on.</summary>
    /// <param name="exception">The exception to report; an <see cref="OutOfMemoryException" /> is treated as fatal instead.</param>
    public static void ReportRecoverable(Exception exception)
    {
        if (IsOutOfMemory(exception))
        {
            ReportFatalAndExit(exception);

            return;
        }

        // An unobserved task exception arrives on the finaliser thread.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ReportRecoverable(exception));

            return;
        }

        if (_noticeIsOpen)
        {
            return;
        }

        string message = $"Something unexpected went wrong:\n{MessageOf(exception)}\n\nFile Auditor will carry on.";

        if (_desktop?.MainWindow is not { } owner)
        {
            // The box pumps messages, so timer ticks and posted callbacks keep running inside it; holding the flag stops a second error opening a second box.
            _noticeIsOpen = true;

            try
            {
                ShowMessageBox(message, IntPtr.Zero);
            }
            finally
            {
                _noticeIsOpen = false;
            }

            return;
        }

        _noticeIsOpen = true;

        _ = ShowNoticeAsync(owner, message);
    }

    /// <summary>Tells the user about an exception the app cannot survive, then ends the process.</summary>
    /// <param name="exception">The exception that ended the app.</param>
    public static void ReportFatalAndExit(Exception exception)
    {
        // A second fatal error on another thread waits here, so the process stays up until the first message has been read.
        if (Interlocked.Exchange(ref _fatalReportStarted, value: 1) is 1)
        {
            Thread.Sleep(Timeout.Infinite);
        }

        ShowMessageBox($"File Auditor ran into a problem it cannot recover from and has to close.\n\n{MessageOf(exception)}", FindOwnerHandle());

        Environment.Exit(exitCode: 1);
    }

    /// <summary>Handles an exception thrown on the UI thread: reports it and carries on, unless it is fatal.</summary>
    /// <param name="sender">The dispatcher that raised the event.</param>
    /// <param name="eventArgs">The unhandled exception and the flag that marks it handled.</param>
    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        if (IsOutOfMemory(eventArgs.Exception))
        {
            ReportFatalAndExit(eventArgs.Exception);

            return;
        }

        eventArgs.Handled = true;

        ReportRecoverable(eventArgs.Exception);
    }

    /// <summary>Handles the exception of a task nobody awaited: marks it observed and reports it.</summary>
    /// <param name="sender">The task that faulted.</param>
    /// <param name="eventArgs">The unobserved exception.</param>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        eventArgs.SetObserved();

        ReportRecoverable(eventArgs.Exception);
    }

    /// <summary>Handles an exception that reached the top of any thread: nothing can recover from it, so it ends the app.</summary>
    /// <param name="sender">The application domain that raised the event.</param>
    /// <param name="eventArgs">The unhandled exception object.</param>
    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        ReportFatalAndExit(eventArgs.ExceptionObject as Exception ?? new InvalidOperationException(eventArgs.ExceptionObject.ToString()));
    }

    /// <summary>Shows the error dialogue over the main window and clears the open-notice flag when it closes. Falls back to the plain message box if the dialogue cannot be shown.</summary>
    /// <param name="owner">The window that owns the dialogue.</param>
    /// <param name="message">The text to show.</param>
    /// <returns>A task that completes when the notice has been closed.</returns>
    private static async Task ShowNoticeAsync(Window owner, string message)
    {
        try
        {
            MessageDialogueViewModel viewModel = new(message, DialogueIcons.Error, PrimaryText: "OK", SecondaryText: null);

            await new MessageDialogueWindow(viewModel).ShowDialog(owner);
        }
        catch (Exception)
        {
            // The dialogue could not be shown, so the plain message box takes over.
            try
            {
                ShowMessageBox(message, IntPtr.Zero);
            }
            catch (Exception)
            {
                // A failure here would fault the discarded task and come back as an unobserved-task report, looping.
            }
        }
        finally
        {
            _noticeIsOpen = false;
        }
    }

    /// <summary>
    ///     Gets the message to show for an exception. A task wraps its failure in an <see cref="AggregateException" />, whose own message is boilerplate, so the first inner message
    ///     is used.
    /// </summary>
    /// <param name="exception">The exception to describe.</param>
    /// <returns>The message the user needs to read.</returns>
    private static string MessageOf(Exception exception)
        => exception is AggregateException aggregate && aggregate.Flatten().InnerExceptions.FirstOrDefault() is { } inner
               ? inner.Message
               : exception.Message;

    /// <summary>Determines whether an exception, or any exception inside an <see cref="AggregateException" />, is an <see cref="OutOfMemoryException" />.</summary>
    /// <param name="exception">The exception to test.</param>
    /// <returns><see langword="true" /> when the app cannot survive it.</returns>
    private static bool IsOutOfMemory(Exception exception)
        => exception is OutOfMemoryException
           || (exception is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(inner => inner is OutOfMemoryException));

    /// <summary>Finds the native handle of the main window, to own the fatal message box.</summary>
    /// <returns>The handle, or <see cref="IntPtr.Zero" /> when it cannot be had safely from the calling thread.</returns>
    private static IntPtr FindOwnerHandle()
    {
        // A window handle from another thread can deadlock against a UI thread that is itself waiting in a second fatal report.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            return IntPtr.Zero;
        }

        try
        {
            return _desktop?.MainWindow?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        }
        catch (Exception)
        {
            // Called from any thread, possibly after the window is gone; no owner is better than another failure.
            return IntPtr.Zero;
        }
    }

    /// <summary>Shows the plain Windows message box. With no owner the box could sit behind other windows, so it asks to be topmost and in the foreground.</summary>
    /// <param name="message">The text to show.</param>
    /// <param name="ownerHandle">The handle of the owning window, or <see cref="IntPtr.Zero" /> for none.</param>
    private static void ShowMessageBox(string message, IntPtr ownerHandle)
        => MessageBoxW(ownerHandle,
                       message,
                       Title,
                       ownerHandle == IntPtr.Zero
                           ? MessageBoxOkWithErrorIcon | MessageBoxSetForeground | MessageBoxTopmost
                           : MessageBoxOkWithErrorIcon);

    /// <summary>Calls the Win32 <c>MessageBoxW</c> function, which shows a modal message box.</summary>
    /// <param name="windowHandle">The handle of the owning window, or <see cref="IntPtr.Zero" /> for none.</param>
    /// <param name="text">The message.</param>
    /// <param name="caption">The caption of the box.</param>
    /// <param name="type">The style flags: buttons, icon and behaviour.</param>
    /// <returns>The identifier of the button the user pressed.</returns>
    [ DllImport(dllName: "user32.dll", CharSet = CharSet.Unicode) ]
    private static extern int MessageBoxW(IntPtr windowHandle, string text, string caption, uint type);
}
