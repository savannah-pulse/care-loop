namespace HisSimulator.Tests;

/// <summary>
/// Locates repository folders from the test output directory.
/// </summary>
internal static class TestPaths
{
    /// <summary>
    /// Absolute path of the shared <c>src/simulators/scenarios</c> folder, found by walking up from the test binaries.
    /// </summary>
    public static string ScenariosDirectory { get; } = FindScenariosDirectory();

    private static string FindScenariosDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "simulators", "scenarios");
            if (File.Exists(Path.Combine(candidate, "scenario.schema.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not find src/simulators/scenarios above " + AppContext.BaseDirectory);
    }
}
