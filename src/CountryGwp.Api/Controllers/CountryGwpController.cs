using CountryGwp.Core.Abstractions;
using CountryGwp.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace CountryGwp.Api.Controllers;

[ApiController]
[Route("server/api/gwp")]
public sealed class CountryGwpController : ControllerBase
{
    private readonly ICountryGwpService _countryGwpService;

    public CountryGwpController(ICountryGwpService countryGwpService)
    {
        _countryGwpService = countryGwpService;
    }

    [HttpPost("avg")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAverageGwpAsync([FromBody] CountryGwpRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Country))
        {
            return BadRequest(new { error = "Country is required." });
        }

        if (request.Lob is null || request.Lob.Count == 0)
        {
            return BadRequest(new { error = "At least one line of business (lob) is required." });
        }

        var result = await _countryGwpService.GetAverageGwpAsync(request.Country, request.Lob, cancellationToken);
        return Ok(result);
    }
}