namespace OpenBrew.Api.Options;

/// <summary>
/// Demo credential store used until a real user/identity provider is introduced.
/// </summary>
public sealed class DemoUserOptions
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
