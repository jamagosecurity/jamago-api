using Jama.Application.Auth;
using Jama.Application.Common.Interfaces;
using Jama.Application.Options;
using Jama.Infrastructure.Data;
using Jama.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jama.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtSettings>(config.GetSection(JwtSettings.SectionName));
        services.Configure<AdminSeedSettings>(config.GetSection(AdminSeedSettings.SectionName));

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDiaInspectionRepository, DiaInspectionRepository>();
        services.AddScoped<ITechnicianInspectionRepository, TechnicianInspectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ApplicationDbContextInitialiser>();

        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
