namespace PatientChannelSimulator.Tests;

/// <summary>Planned WS8 behaviours: scenario loader.</summary>
public class ScenarioLoaderTests
{
    [Fact(Skip = "WS8: not implemented")]
    public void List_returns_every_scenario_except_the_schema_file() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Load_returns_null_for_an_unknown_scenario() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Load_rejects_a_name_that_could_escape_the_directory() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Load_rejects_a_file_that_is_not_marked_synthetic() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Load_rejects_a_file_whose_name_differs_from_its_file_name() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Relative_directory_is_resolved_against_the_content_root() { }
}
