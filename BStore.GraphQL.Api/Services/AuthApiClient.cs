using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.GraphQL.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Services;

/// <summary>
/// Typed HttpClient for B-store token validation.
/// Maps to POST /v2/b-stores/validate-token.
/// </summary>
public sealed class AuthApiClient : IAuthApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AuthApiClient> _log;
    private readonly ZnodeApiOptions _opts;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuthApiClient(
        HttpClient http,
        IOptions<ZnodeApiOptions> opts,
        ILogger<AuthApiClient> log)
    {
        _http = http;
        _opts = opts.Value;
        _log  = log;
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────

    private sealed class ValidateTokenApiResponse
    {
        public bool                  HasError             { get; set; }
        public string?               ErrorMessage         { get; set; }
        public BStoreAccessDetailsDto? BStoresAccessDetails { get; set; }
    }

    private sealed class BStoreAccessDetailsDto
    {
        public string? Token           { get; set; }
        public string? FirstName       { get; set; }
        public int?    WebstoreUserId  { get; set; }
        public int?    PortalId        { get; set; }
        public string? ParentStoreName { get; set; }
    }

    // ── Implementation ────────────────────────────────────────────────────────

    public async Task<BStoreValidateTokenResult?> ValidateTokenAsync(string token, CancellationToken ct)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", _opts.BasicAuthHeader);
        try
        {
            var body = new { Token = token };
            using var content = JsonContent.Create(body, options: _json);
            var resp = await _http.PostAsync("v2/b-stores/validate-token", content, ct);
            resp.EnsureSuccessStatusCode();
            var apiResp = await resp.Content.ReadFromJsonAsync<ValidateTokenApiResponse>(_json, ct);
            if (apiResp is null)
                return new BStoreValidateTokenResult { HasError = true, ErrorMessage = "No response from auth service." };

            if (apiResp.HasError || apiResp.BStoresAccessDetails is null)
                return new BStoreValidateTokenResult
                {
                    HasError     = true,
                    ErrorMessage = apiResp.ErrorMessage ?? "Token is invalid or expired."
                };

            var d = apiResp.BStoresAccessDetails;
            return new BStoreValidateTokenResult
            {
                HasError      = false,
                AccessDetails = new BStoreAccessDetails
                {
                    Token           = d.Token ?? token,
                    FirstName       = d.FirstName,
                    WebstoreUserId  = d.WebstoreUserId,
                    PortalId        = d.PortalId,
                    ParentStoreName = d.ParentStoreName
                }
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ValidateTokenAsync failed");
            throw;
        }
    }
}
