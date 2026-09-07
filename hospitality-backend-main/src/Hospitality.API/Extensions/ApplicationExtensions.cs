using Hospitality.Application;
using Hospitality.Infrastructure;
using Hospitality.Infrastructure.Persistence;

namespace Hospitality.API.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddApplication();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await dbInitializer.InitializeAsync();
    }
}