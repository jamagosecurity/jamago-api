using Jama.Application.Interfaces;
using Jama.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Jama.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IContactService, ContactService>();
        return services;
    }
}
