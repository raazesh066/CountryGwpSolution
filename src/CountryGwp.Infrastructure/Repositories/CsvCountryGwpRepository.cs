using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CountryGwp.Core.Abstractions;
using CountryGwp.Core.Models;
using CountryGwp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CountryGwp.Infrastructure.Repositories;

public sealed class CsvCountryGwpRepository : ICountryGwpRepository
{
    private static readonly Regex YearRegex = new(@"(20\d{2})", RegexOptions.Compiled);

    private readonly CountryGwpDataOptions _dataOptions;
    private readonly ILogger<CsvCountryGwpRepository> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private List<GwpRecord>? _records;

    public CsvCountryGwpRepository(
        IOptions<CountryGwpDataOptions> dataOptions,
        ILogger<CsvCountryGwpRepository> logger)
    {
        _dataOptions = dataOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<GwpRecord>> GetByCountryAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var normalizedCountry = Normalize(countryCode);
        var records = _records!
            .Where(record => record.Country.Equals(normalizedCountry, StringComparison.Ordinal))
            .ToArray();

        return records;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_records is not null)
        {
            return;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_records is not null)
            {
                return;
            }

            if (!File.Exists(_dataOptions.CsvPath))
            {
                throw new FileNotFoundException($"CSV file not found at path '{_dataOptions.CsvPath}'.");
            }

            _records = await LoadRecordsAsync(_dataOptions.CsvPath, cancellationToken);
            _logger.LogInformation("Loaded {RecordCount} GWP records from CSV", _records.Count);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static async Task<List<GwpRecord>> LoadRecordsAsync(string csvPath, CancellationToken cancellationToken)
    {
        var records = new List<GwpRecord>();

        using var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        using var reader = new StreamReader(stream);

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return records;
        }

        var headers = ParseCsvLine(headerLine);
        var countryIndex = FindCountryColumn(headers);
        var lobIndex = FindLobColumn(headers);
        var yearColumns = BuildYearColumns(headers);
        var metricMap = BuildMetricMap(headers, countryIndex);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = ParseCsvLine(line);
            if (countryIndex < 0 || countryIndex >= cells.Count)
            {
                continue;
            }

            var country = Normalize(cells[countryIndex]);
            if (string.IsNullOrWhiteSpace(country))
            {
                continue;
            }

            if (lobIndex >= 0 && lobIndex < cells.Count && yearColumns.Count > 0)
            {
                var lob = Normalize(cells[lobIndex]);
                if (string.IsNullOrWhiteSpace(lob))
                {
                    continue;
                }

                foreach (var (index, year) in yearColumns)
                {
                    if (index >= cells.Count)
                    {
                        continue;
                    }

                    var raw = cells[index].Trim();
                    if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        continue;
                    }

                    records.Add(new GwpRecord(country, lob, year, value));
                }

                continue;
            }

            foreach (var (index, lob, year) in metricMap)
            {
                if (index >= cells.Count)
                {
                    continue;
                }

                var raw = cells[index].Trim();
                if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                records.Add(new GwpRecord(country, lob, year, value));
            }
        }

        return records;
    }

    private static List<(int Index, string Lob, int Year)> BuildMetricMap(IReadOnlyList<string> headers, int countryIndex)
    {
        var result = new List<(int Index, string Lob, int Year)>();

        for (var i = 0; i < headers.Count; i++)
        {
            if (i == countryIndex)
            {
                continue;
            }

            var header = headers[i];
            var yearMatch = YearRegex.Match(header);
            if (!yearMatch.Success)
            {
                continue;
            }

            if (!int.TryParse(yearMatch.Groups[1].Value, out var year) || year < 2008 || year > 2015)
            {
                continue;
            }

            var lobRaw = header.Replace(yearMatch.Value, string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("gwp", string.Empty, StringComparison.OrdinalIgnoreCase);

            var lob = Normalize(lobRaw);
            if (string.IsNullOrWhiteSpace(lob))
            {
                continue;
            }

            result.Add((i, lob, year));
        }

        return result;
    }

    private static int FindLobColumn(IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var normalized = Normalize(headers[i]);
            if (normalized.Contains("lineofbusiness", StringComparison.Ordinal) ||
                normalized.Equals("lob", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<(int Index, int Year)> BuildYearColumns(IReadOnlyList<string> headers)
    {
        var result = new List<(int Index, int Year)>();

        for (var i = 0; i < headers.Count; i++)
        {
            var yearMatch = YearRegex.Match(headers[i]);
            if (!yearMatch.Success)
            {
                continue;
            }

            if (!int.TryParse(yearMatch.Groups[1].Value, out var year) || year < 2008 || year > 2015)
            {
                continue;
            }

            result.Add((i, year));
        }

        return result;
    }

    private static int FindCountryColumn(IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var normalized = Normalize(headers[i]);
            if (normalized.Contains("country", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return 0;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
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