using CountryGwp.Core.Abstractions;
using CountryGwp.Infrastructure.Options;
using CountryGwp.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CountryGwp.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCountryGwpInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CountryGwpDataOptions>(configuration.GetSection(CountryGwpDataOptions.SectionName));
        services.AddSingleton<ICountryGwpRepository, CsvCountryGwpRepository>();
        return services;
    }
}