namespace CountryGwp.Core.Models;

public sealed class CountryGwpRequest
{
    public string Country { get; init; } = string.Empty;

    public List<string> Lob { get; init; } = [];
}