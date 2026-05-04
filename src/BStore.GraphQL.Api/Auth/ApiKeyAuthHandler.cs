using System.Security.Claims;
using System.Text.Encodings.Web;
using BStore.GraphQL.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Authentication handler for server-to-server API key requests.
/// Validates the <c>X-API-Key</c> header against configured keys.
/// </summary>
public sealed class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AuthSettings _authSettings;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AuthSettings> authSettings)
        : base(options, logger, encoder)
    {
        _authSettings = authSettings.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthConstants.ApiKeyHeader, out var apiKeyHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var apiKey = apiKeyHeader.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        var configured = _authSettings.ApiKeys;
        if (configured is null || configured.Length == 0 || !configured.Contains(apiKey))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "api-client"),
            new Claim(AuthConstants.ClaimRole, AuthConstants.RoleServerToServer),
            new Claim(ClaimTypes.Role, AuthConstants.RoleServerToServer)
        };

        var identity = new ClaimsIdentity(claims, AuthConstants.ApiKeyScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthConstants.ApiKeyScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
