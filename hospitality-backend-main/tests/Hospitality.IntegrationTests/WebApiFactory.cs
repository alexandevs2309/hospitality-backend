using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Hospitality.IntegrationTests;

/// <summary>
/// Host de la API real (Program) apuntando a una base PostgreSQL dedicada de pruebas.
/// La migración y el seed corren automáticamente en el arranque del host.
/// </summary>
public class WebApiFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString = TestDatabase.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestDatabase.EnsureFreshDatabase();

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Seed:DemoData"] = "true"
            });
        });
    }
}