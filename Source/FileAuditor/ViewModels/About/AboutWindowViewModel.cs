namespace FileAuditor.ViewModels.About;

/// <summary>The About box: the product name, version and copyright, read from the assembly.</summary>
public sealed class AboutWindowViewModel
{
    /// <summary>Initialises a new instance reading the product details from the executing assembly.</summary>
    public AboutWindowViewModel()
    {
        Assembly assembly = typeof(AboutWindowViewModel).Assembly;

        Version? version = assembly.GetName()
                                   .Version;

        ProductName = assembly.GetCustomAttribute<AssemblyProductAttribute>()
                              ?.Product
                      ?? "File Auditor";

        Version = $"Version {version?.ToString(fieldCount: 3) ?? "1.0.0"}";

        Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()
                            ?.Copyright
                    ?? "Copyright © 2026 MediQuant";
    }

    /// <summary>Gets the copyright line.</summary>
    public string Copyright { get; }

    /// <summary>Gets the product name.</summary>
    public string ProductName { get; }

    /// <summary>Gets the version line, for example <c>Version 1.0.0</c>.</summary>
    public string Version { get; }
}
