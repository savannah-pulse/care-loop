namespace CareLoop.Api.Endpoints;

/// <summary>
/// A group of related routes. Implement this in its own file and it is mapped automatically, so engineers
/// working on different stages never edit the same Program.cs lines.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder app);
}

public static class EndpointModuleExtensions
{
    public static IEndpointRouteBuilder MapEndpointModules(this IEndpointRouteBuilder app)
    {
        var modules = typeof(IEndpointModule).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpointModule).IsAssignableFrom(t));

        foreach (var module in modules)
        {
            ((IEndpointModule)Activator.CreateInstance(module)!).MapEndpoints(app);
        }

        return app;
    }
}
