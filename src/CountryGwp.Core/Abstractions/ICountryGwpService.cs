namespace CountryGwp.Core.Abstractions;

public interface ICountryGwpService
{
    Task<IReadOnlyDictionary<string, decimal>> GetAverageGwpAsync(
        string countryCode,
        IReadOnlyCollection<string> lobs,
        CancellationToken cancellationToken = default);
}