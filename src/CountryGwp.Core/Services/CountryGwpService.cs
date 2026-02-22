using CountryGwp.Core.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CountryGwp.Core.Services;

public sealed class CountryGwpService : ICountryGwpService
{
    private const int StartYear = 2008;
    private const int EndYear = 2015;

    private readonly ICountryGwpRepository _repository;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CountryGwpService> _logger;

    public CountryGwpService(
        ICountryGwpRepository repository,
        IMemoryCache memoryCache,
        ILogger<CountryGwpService> logger)
    {
        _repository = repository;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetAverageGwpAsync(
        string countryCode,
        IReadOnlyCollection<string> lobs,
        CancellationToken cancellationToken = default)
    {
        var normalizedCountry = Normalize(countryCode);
        var normalizedLobs = lobs
            .Select(Normalize)
            .Where(static lob => !string.IsNullOrWhiteSpace(lob))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static lob => lob, StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(normalizedCountry) || normalizedLobs.Length == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        var cacheKey = $"gwp:{normalizedCountry}:{string.Join(',', normalizedLobs)}";
        if (_memoryCache.TryGetValue<IReadOnlyDictionary<string, decimal>>(cacheKey, out var cachedResult))
        {
            return cachedResult!;
        }

        var records = await _repository.GetByCountryAsync(normalizedCountry, cancellationToken);

        var lookup = records
            .Where(record => record.Year >= StartYear && record.Year <= EndYear)
            .GroupBy(record => record.Lob, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(record => record.Value).ToArray(),
                StringComparer.Ordinal);

        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var lob in normalizedLobs)
        {
            if (!lookup.TryGetValue(lob, out var values) || values.Length == 0)
            {
                result[lob] = 0m;
                continue;
            }

            var avg = values.Average();
            result[lob] = Math.Round(avg, 1, MidpointRounding.AwayFromZero);
        }

        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
        _logger.LogInformation("Calculated GWP average for country {Country} and {LobCount} LOB entries", normalizedCountry, normalizedLobs.Length);

        return result;
    }

    private static string Normalize(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }
}