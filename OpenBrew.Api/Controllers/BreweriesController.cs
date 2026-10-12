using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenBrew.Api.Contracts;
using OpenBrew.Api.Services;

namespace OpenBrew.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/breweries")]
[Authorize]
[EnableRateLimiting("BreweryApi")]
public sealed class BreweriesController : ControllerBase
{
    private readonly IBreweryService _breweryService;
    private readonly ILogger<BreweriesController> _logger;

    public BreweriesController(IBreweryService breweryService, ILogger<BreweriesController> logger)
    {
        _breweryService = breweryService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BreweryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<BreweryResponse>>> GetBreweries(
        [FromQuery] BreweryQueryRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling GetBreweries request: Page={Page}, PageSize={PageSize}, SortBy={SortBy}",
            request.Page, request.PageSize, request.SortBy);

        var breweries = await _breweryService.GetBreweriesAsync(request, cancellationToken);

        _logger.LogInformation("Returning {Count} breweries", breweries.Count);
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
        _logger.LogInformation("Handling Autocomplete request: Term={Term}", request.Term);

        var suggestions = await _breweryService.AutocompleteAsync(request, cancellationToken);

        _logger.LogInformation("Returning {Count} autocomplete suggestions", suggestions.Count);
        return Ok(suggestions);
    }
}