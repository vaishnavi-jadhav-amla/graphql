using System.Threading.RateLimiting;
using BStore.GraphQL.Api.Configuration;
using Microsoft.AspNetCore.RateLimiting;

namespace BStore.GraphQL.Api.Infrastructure;

/// <summary>
/// Registers HTTP-level rate limiting policies (global, mutations, auth, admin).
/// </summary>
public static class RateLimitingRegistration
{
    public const string PolicyGlobal = "graphql-global";
    public const string PolicyMutations = "graphql-mutations";
    public const string PolicyAuth = "graphql-auth";
    public const string PolicyAdmin = "graphql-admin";

    public static WebApplicationBuilder AddBStoreRateLimiting(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration
            .GetSection(RateLimitSettings.Section)
            .Get<RateLimitSettings>() ?? new RateLimitSettings();

        builder.Services.Configure<RateLimitSettings>(
            builder.Configuration.GetSection(RateLimitSettings.Section));

        if (!settings.Enabled) return builder;

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyGlobal, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.GlobalRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(PolicyMutations, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.MutationRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(PolicyAuth, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AuthRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(PolicyAdmin, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.AdminRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        return builder;
    }
}
