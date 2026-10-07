using System.ComponentModel.DataAnnotations;

namespace OpenBrew.Api.Contracts;

public sealed record AutocompleteQueryRequest
{
    /// <summary>
    /// Search prefix used to find matching brewery names.
    /// </summary>
    [Required]
    [MinLength(1)]
    public required string Term { get; init; }

    /// <summary>
    /// Maximum number of suggestions.
    /// </summary>
    [Range(1, 50)]
    public int Limit { get; init; } = 10;
}