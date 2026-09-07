using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

namespace Hospitality.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar JWT Authentication
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["Secret"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("JwtSettings:Secret no está configurado. Debe definirse mediante variable de entorno JwtSettings__Secret en entornos distintos de Development.");
        }
        if (Encoding.UTF8.GetByteCount(secretKey) < 32)
        {
            throw new InvalidOperationException("JwtSettings:Secret debe tener al menos 256 bits (32 bytes) para garantizar la seguridad de la firma HMAC.");
        }
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };
            
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        // Configurar Swagger con autenticación JWT
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Hospitality API - Plataforma de Gestión Hotelera",
                Version = "v1",
                Description = "API completa para gestión de hoteles, habitaciones, reservas, huéspedes y operaciones",
                Contact = new OpenApiContact
                {
                    Name = "Hospitality Team",
                    Email = "support@hospitality.com"
                },
                License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // Configurar seguridad JWT
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization usando esquema Bearer. Ejemplo: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header
                    },
                    new List<string>()
                }
            });

            // Ordenar por controlador
            c.OrderActionsBy((apiDesc) => $"{apiDesc.ActionDescriptor.RouteValues["controller"]}");
            
            // Agrupar por tags
            c.TagActionsBy(api => new[] { api.GroupName });
            
            // Configurar schemas para enums como strings
            c.UseInlineDefinitionsForEnums();
        });

        // Configurar health checks
        services.AddHealthChecks();

        // Configurar rate limiting para endpoints sensibles (login/register/forgot-password)
        var rateLimitingEnabled = configuration.GetValue("RateLimiting:Enable", false);
        if (rateLimitingEnabled)
        {
            var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
            var windowSeconds = configuration.GetValue("RateLimiting:Window", 60);
            var queueLimit = configuration.GetValue("RateLimiting:QueueLimit", 10);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("auth", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = TimeSpan.FromSeconds(windowSeconds),
                            QueueLimit = queueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                        }));
            });
        }

        return services;
    }
}