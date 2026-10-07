using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenBrew.Api.Contracts;
using OpenBrew.Api.Services;

namespace OpenBrew.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/breweries")]
public sealed class BreweriesController : ControllerBase
{
    private readonly IBreweryService _breweryService;

    public BreweriesController(IBreweryService breweryService)
    {
        _breweryService = breweryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BreweryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<BreweryResponse>>> GetBreweries(
        [FromQuery] BreweryQueryRequest request,
        CancellationToken cancellationToken)
    {
        var breweries = await _breweryService.GetBreweriesAsync(request, cancellationToken);
        return Ok(breweries);
    }

    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<string>>> Autocomplete(
        [FromQuery] AutocompleteQueryRequest request,
        CancellationToken cancellationToken)
    {
        var suggestions = await _breweryService.AutocompleteAsync(request, cancellationToken);
        return Ok(suggestions);
    }
}