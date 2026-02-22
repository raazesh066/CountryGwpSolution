using CountryGwp.Core.Abstractions;
using CountryGwp.Core.Models;
using CountryGwp.Core.Services;
using CountryGwp.Infrastructure.Options;
using CountryGwp.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CountryGwp.Tests;

public class CountryGwpServiceTests
{
    [Test]
    public async Task GetAverageGwpAsync_ReturnsAveragesAndCachesResult()
    {
        var repository = new FakeRepository(
        [
            new GwpRecord("ae", "property", 2008, 100m),
            new GwpRecord("ae", "property", 2009, 200m),
            new GwpRecord("ae", "transport", 2008, 300m),
            new GwpRecord("ae", "transport", 2009, 500m)
        ]);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CountryGwpService(repository, cache, NullLogger<CountryGwpService>.Instance);

        var firstResult = await service.GetAverageGwpAsync("ae", ["property", "transport"]);
        var secondResult = await service.GetAverageGwpAsync("ae", ["transport", "property"]);

        Assert.Multiple(() =>
        {
            Assert.That(firstResult["property"], Is.EqualTo(150m));
            Assert.That(firstResult["transport"], Is.EqualTo(400m));
            Assert.That(secondResult["property"], Is.EqualTo(150m));
            Assert.That(repository.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CsvRepository_ExtractsCountryAndLobValues()
    {
        var tempFilePath = Path.GetTempFileName();
        try
        {
            var csv = string.Join(Environment.NewLine,
            [
                "country,property_2008,property_2009,transport_2008",
                "ae,10,20,30",
                "us,40,50,60"
            ]);
            await File.WriteAllTextAsync(tempFilePath, csv);

            var options = Options.Create(new CountryGwpDataOptions { CsvPath = tempFilePath });
            var repository = new CsvCountryGwpRepository(options, NullLogger<CsvCountryGwpRepository>.Instance);

            var aeRows = await repository.GetByCountryAsync("ae");

            Assert.That(aeRows.Count, Is.EqualTo(3));
            Assert.That(aeRows.Any(row => row.Lob == "property" && row.Year == 2008 && row.Value == 10m), Is.True);
            Assert.That(aeRows.Any(row => row.Lob == "property" && row.Year == 2009 && row.Value == 20m), Is.True);
            Assert.That(aeRows.Any(row => row.Lob == "transport" && row.Year == 2008 && row.Value == 30m), Is.True);
        }
        finally
        {
            File.Delete(tempFilePath);
        }
    }

    private sealed class FakeRepository : ICountryGwpRepository
    {
        private readonly IReadOnlyCollection<GwpRecord> _records;

        public FakeRepository(IReadOnlyCollection<GwpRecord> records)
        {
            _records = records;
        }

        public int CallCount { get; private set; }

        public Task<IReadOnlyCollection<GwpRecord>> GetByCountryAsync(string countryCode, CancellationToken cancellationToken = default)
        {
            CallCount++;

            var result = _records.Where(record => record.Country.Equals(countryCode, StringComparison.Ordinal)).ToArray();
            return Task.FromResult<IReadOnlyCollection<GwpRecord>>(result);
        }
    }
}