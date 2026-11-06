namespace FileAuditor.Common.Extensions;

/// <summary>Helpers for the Classic window chrome.</summary>
public static class ClassicWindowExtensions
{
    /// <summary>
    ///     Hides the maximise/restore caption button, which Classic.Avalonia's template draws even on a fixed-size window. The button lives in a nested template,
    ///     so call this once the window has opened and its visual tree exists.
    /// </summary>
    /// <param name="window">The window whose caption buttons are adjusted.</param>
    public static void HideMaximiseButton(this Window window)
    {
        Control? button = window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "PART_RestoreButton");

        if (button is not null)
        {
            button.IsVisible = false;
        }
    }
}
