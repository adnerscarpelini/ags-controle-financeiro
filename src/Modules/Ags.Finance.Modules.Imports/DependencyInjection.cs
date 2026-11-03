using Ags.Finance.Modules.Imports.Application.Registries;
using Ags.Finance.Modules.Imports.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Ags.Finance.Modules.Imports;

public static class DependencyInjection
{
    public static IServiceCollection AddImportsModule(this IServiceCollection services)
    {
        services.AddScoped<IRegistryExport, RegistryExporter>();
        return services;
    }
}
