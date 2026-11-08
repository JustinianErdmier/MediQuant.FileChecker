namespace FileAuditor.ViewModels.Main;

/// <summary>
///     One run of an audit in one direction, as the main window drives it: the window times the run and refreshes the Audit group on a timer, and the run supplies the direction's
///     progress, its runner and how its outcome reads. It keeps the per-direction code out of <see cref="MainWindowViewModel" />.
/// </summary>
/// <param name="audit">The Audit group that shows this run.</param>
public abstract class AuditRun(AuditStatusBase audit)
{
    /// <summary>Gets the Audit group that shows this run.</summary>
    public AuditStatusBase Audit { get; } = audit;

    /// <summary>Gets the status-bar text for the phase the run is in.</summary>
    public abstract string Phase { get; }

    /// <summary>Refreshes the Audit group's statistics from the run's progress.</summary>
    /// <param name="elapsed">The time since the run started.</param>
    /// <param name="isFinished">Whether the run has ended.</param>
    public abstract void Update(TimeSpan elapsed, bool isFinished);

    /// <summary>Runs the audit off the UI thread, so its continuations never queue behind the dispatcher.</summary>
    /// <param name="ct">Stops the run.</param>
    /// <returns>The final status for the status bar, and an error message for a dialogue when the run failed.</returns>
    public async Task<(string Status, string? Error)> ExecuteAsync(CancellationToken ct)
    {
        try
        {
            return await Task.Run(() => RunAsync(ct), CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return DescribeError(Error.Unexpected(code: "Audit.Unexpected", ex.Message));
        }
    }

    /// <summary>Runs the audit and describes how it ended.</summary>
    /// <param name="ct">Stops the run.</param>
    /// <returns>The final status and the error message, as <see cref="ExecuteAsync" /> returns them.</returns>
    protected abstract Task<(string Status, string? Error)> RunAsync(CancellationToken ct);

    /// <summary>Describes an audit that ended in an error.</summary>
    /// <param name="error">The first error returned.</param>
    /// <returns>The final status and the message for the error dialogue (none when the user stopped the run before it began checking).</returns>
    protected static (string Status, string? Error) DescribeError(Error error)
    {
        if (error.Code == AuditErrors.StoppedBeforeCheckingCode)
        {
            return ("Stopped", null);
        }

        bool readFailed = error.Code is SqlErrors.QueryFailedCode or AuditErrors.ReadFailedCode;

        return ("Failed", readFailed ? $"The audit could not read the file index table:\n\n{error.Description}" : error.Description);
    }

    /// <summary>Describes an audit that wrote its report.</summary>
    /// <param name="result">How the run ended.</param>
    /// <param name="failureMessage">The failure message, used when the run failed part-way.</param>
    /// <returns>The final status and the message for the error dialogue (none unless the run failed).</returns>
    protected static (string Status, string? Error) DescribeResult(AuditResultKind result, string? failureMessage)
        => result switch
        {
            AuditResultKind.Completed     => ("Completed", null),
            AuditResultKind.StoppedByUser => ("Stopped", null),
            var _                         => ("Failed", $"The audit failed part-way through:\n\n{failureMessage}\n\nThe report so far has been written.")
        };
}
