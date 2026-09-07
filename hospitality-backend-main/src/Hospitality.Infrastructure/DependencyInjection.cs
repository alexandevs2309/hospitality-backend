using Hospitality.Application.Auth.Commands;
using Hospitality.Application.Auth.Services;
using Hospitality.Application.Common.Interfaces;
using Hospitality.Application.Hotels.Commands;
using Hospitality.Application.Hotels.Queries;
using Hospitality.Application.Hotels.Services;
using Hospitality.Application.Rooms.Commands;
using Hospitality.Application.Rooms.Services;
using Hospitality.Domain.Entities;
using Hospitality.Infrastructure.Persistence;
using Hospitality.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hospitality.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurar DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection no está configurado. Debe definirse mediante variable de entorno ConnectionStrings__DefaultConnection en entornos distintos de Development.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Configurar Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            // Configuración de contraseña
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            
            // Configuración de usuario
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            
            // Configuración de bloqueo
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Configurar HttpContextAccessor para CurrentUserService
        services.AddHttpContextAccessor();

        // Registrar IApplicationDbContext
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Inicializador de base de datos (roles, usuario admin y estructura)
        services.AddScoped<DatabaseInitializer>();

        // Registrar servicios de aplicación (solo los que existen)
        services.AddScoped<IHotelService, HotelService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoomService, RoomService>();
        // DashboardService implementa IDashboardService de Hotels.Queries (contrato del dashboard actual)
        services.AddScoped<IDashboardService, DashboardService>();

        // Registrar servicios de dominio básicos
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IHotelAccessGuard, HotelAccessGuard>();

        return services;
    }
}