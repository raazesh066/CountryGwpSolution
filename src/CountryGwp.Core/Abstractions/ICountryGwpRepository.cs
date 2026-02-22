using CountryGwp.Core.Models;

namespace CountryGwp.Core.Abstractions;

public interface ICountryGwpRepository
{
    Task<IReadOnlyCollection<GwpRecord>> GetByCountryAsync(string countryCode, CancellationToken cancellationToken = default);
}