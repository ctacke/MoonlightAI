namespace MoonlightAI.Core.Configuration;

/// <summary>
/// Configuration for database storage.
/// </summary>
public class DatabaseConfiguration
{
    public const string SectionName = "Database";

    /// <summary>
    /// Path to the SQLite database file.
    /// </summary>
    public string DatabasePath { get; set; } = "./moonlight.db";

    /// <summary>
    /// Whether to enable detailed logging for database operations.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;
}
