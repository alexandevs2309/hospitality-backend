using Hospitality.API.Extensions;
using Hospitality.API.Middleware;
using Hospitality.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Serilog;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/hospitality-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationExceptionFilter>();
});
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddSingleton<Hospitality.API.Hubs.HotelSubscriptionRegistry>();
builder.Services.AddHostedService<Hospitality.API.Hubs.DashboardBroadcaster>();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddApiServices(builder.Configuration);

// Configurar CORS para integración con frontend Angular
builder.Services.AddCors(options =>
{
    // Policy para desarrollo (localhost)
    options.AddPolicy("AngularDev",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .WithExposedHeaders("Content-Disposition");
        });
    
    // Policy para producción (orígenes configurados)
    options.AddPolicy("AngularProd",
        policy =>
        {
            // Obtener orígenes permitidos desde configuración
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                ?? new[] { "http://localhost:4200", "https://localhost:4200" };
            
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .WithExposedHeaders("Content-Disposition");
        });
});

var app = builder.Build();

// Configurar pipeline HTTP
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AngularDev");
}
else
{
    // En producción, usar política configurada
    app.UseCors("AngularProd");
    
    // También habilitar Swagger en producción si está configurado
    var enableSwaggerInProd = builder.Configuration.GetValue<bool>("EnableSwaggerInProduction", false);
    if (enableSwaggerInProd)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hospitality API v1");
            c.RoutePrefix = "api-docs";
            c.DocumentTitle = "Hospitality API Documentation";
        });
    }
}

app.UseHttpsRedirection();
if (app.Configuration.GetValue("RateLimiting:Enable", false))
{
    app.UseRateLimiter();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<Hospitality.API.Hubs.DashboardLiveHub>("/hubs/dashboard");

// Inicializar base de datos
await app.InitializeDatabaseAsync();

app.Run();

// Punto de entrada expuesto para las pruebas de integración (WebApplicationFactory<Program>).
public partial class Program { }

// Punto de entrada para pruebas de integración (WebApplicationFactory<Program>).
public partial class Program { }