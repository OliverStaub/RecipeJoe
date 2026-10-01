namespace RecipeJoe.Api.Import;

/// <summary>Bound from the "Import" configuration section.</summary>
internal sealed class ImportOptions
{
    /// <summary>Hosts exempt from the SSRF guard: `fixtures` in E2E, `localhost` in Development, loopback in tests.</summary>
    public string[] AllowedHosts { get; set; } = [];

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    public int MaxBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>How many Imports <see cref="Imports.ImportRunner"/> runs at once.</summary>
    public int MaxConcurrent { get; set; } = 2;
}
