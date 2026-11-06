namespace FileAuditor.Common.Serialisation;

/// <summary>Source-generated JSON metadata for <see cref="AppSettings" />. Enums are written as strings and the output is indented.</summary>
[ JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true) ]
[ JsonSerializable(typeof(AppSettings)) ]
public sealed partial class SettingsJsonContext : JsonSerializerContext;
