namespace FileAuditor.ViewModels.About;

/// <summary>The About box: the product name, version and copyright, read from the assembly.</summary>
public sealed class AboutWindowViewModel
{
    /// <summary>Initialises a new instance reading the product details from the executing assembly.</summary>
    /// <exception cref="UnreachableException">
    ///     If one or more product information cannot be retrieved from the assembly, then an <see cref="UnreachableException" /> is thrown. If
    ///     something has happened that is preventing the assembly information to be read, then that is a sign of a bigger issue, and the app doesn't need to build/launch. However,
    ///     assuming the app is compiled and published as a self-contained single executable, then this really should never happen, hence them being 'unreachable'.
    /// </exception>
    public AboutWindowViewModel()
    {
        Assembly assembly = typeof(AboutWindowViewModel).Assembly;

        Version? assemblyVersion = assembly.GetName()
                                           .Version;

        string version = assemblyVersion?.ToString(fieldCount: 3) ?? throw new UnreachableException(message: "Unable to determine version number");

        ProductName = assembly.GetCustomAttribute<AssemblyProductAttribute>()
                              ?.Product
                      ?? throw new UnreachableException(message: "Unable to determine product name");

        Version = $"Version {version}";

        Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()
                            ?.Copyright
                    ?? throw new UnreachableException(message: "Unable to determine copyright");
    }

    /// <summary>Gets the copyright line.</summary>
    public string Copyright { get; }

    /// <summary>Gets the product name.</summary>
    public string ProductName { get; }

    /// <summary>Gets the version line, for example <c>Version 1.0.0</c>.</summary>
    public string Version { get; }
}
