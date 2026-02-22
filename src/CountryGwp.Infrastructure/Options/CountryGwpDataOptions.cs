namespace CountryGwp.Infrastructure.Options;

public sealed class CountryGwpDataOptions
{
    public const string SectionName = "Data";

    public string CsvPath { get; init; } = @"C:\Users\rajeshyadav\Downloads\gwpByCountry (2).csv";
}