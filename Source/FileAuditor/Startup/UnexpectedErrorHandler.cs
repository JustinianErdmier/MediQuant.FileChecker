namespace FileAuditor.Startup;

/// <summary>
///     Catches exceptions nothing else handled. One the app can survive is shown in an error dialogue and the app carries on; one it cannot survive is explained in a message
///     box before the process exits. Nothing is written to disk.
/// </summary>
public static class UnexpectedErrorHandler
{
    private const string Title = "File Auditor";

    private const uint MessageBoxOkWithErrorIcon = 0x10;

    private const uint MessageBoxSetForeground = 0x10000;

    private const uint MessageBoxTopmost = 0x40000;

    private static IClassicDesktopStyleApplicationLifetime? _desktop;

    // Touched only on the UI thread, so it needs no lock.
    private static bool _noticeIsOpen;

    private static int _fatalReportStarted;

    private static bool _isInstalled;

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

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException  += OnUnobservedTaskException;
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
                ShowMessageBox(message, ownerHandle: IntPtr.Zero);
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

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        eventArgs.SetObserved();

        ReportRecoverable(eventArgs.Exception);
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        ReportFatalAndExit(eventArgs.ExceptionObject as Exception ?? new InvalidOperationException(eventArgs.ExceptionObject.ToString()));
    }

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
                ShowMessageBox(message, ownerHandle: IntPtr.Zero);
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

    // A task wraps its failure in an AggregateException, whose own message is boilerplate; the user needs the real one.
    private static string MessageOf(Exception exception)
        => exception is AggregateException aggregate && aggregate.Flatten().InnerExceptions.FirstOrDefault() is { } inner
               ? inner.Message
               : exception.Message;

    private static bool IsOutOfMemory(Exception exception)
        => exception is OutOfMemoryException
           || exception is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(inner => inner is OutOfMemoryException);

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

    // With no owner the box could sit behind other windows, so it asks to be topmost and in the foreground.
    private static void ShowMessageBox(string message, IntPtr ownerHandle)
        => MessageBoxW(ownerHandle,
                       message,
                       Title,
                       ownerHandle == IntPtr.Zero
                           ? MessageBoxOkWithErrorIcon | MessageBoxSetForeground | MessageBoxTopmost
                           : MessageBoxOkWithErrorIcon);

    [ DllImport("user32.dll", CharSet = CharSet.Unicode) ]
    private static extern int MessageBoxW(IntPtr windowHandle, string text, string caption, uint type);
}
