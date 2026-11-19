using CareLoop.Api.Infrastructure.Ai;

namespace CareLoop.Tests.Fakes;

/// <summary>The prompt files the API ships, as the tests' build output holds them.</summary>
public static class TestPrompts
{
    /// <summary>The folder the shipped prompt files are copied to.</summary>
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Prompts");

    /// <summary>The shipped prompts, read once.</summary>
    public static AiPrompts Shipped { get; } = new(Directory);
}
