using BStore.GraphQL.Api.Application;
using BStore.GraphQL.Api.Auth.FieldPermissions;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.GraphQL.Types;
using BStore.GraphQL.Api.Messaging;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.Logging;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;
using ErrorCodes = BStore.GraphQL.Api.Common.ErrorCodes;

namespace BStore.GraphQL.Api.GraphQL.Mutations;

/// <summary>
/// B-store mutation root. Resolvers call <see cref="IBStoreApplicationService"/> (EF against Znode_Entities; no B-store HTTP client).
/// On success, related cache entries are invalidated.
/// </summary>
/// <remarks>
/// All mutations require at least BStoreAdmin role (Admin, BStoreOwner, or ServerToServer).
/// </remarks>
public sealed class BStoreMutation(
    ILogger<BStoreMutation> logger,
    ICacheService cache,
    IEventPublisher events)
{
    // ── POST /v2/b-stores/parent-portal/{portalId}/users/{userId}/setup ───────

    /// <summary>Creates a new B-store under a parent portal for a user.</summary>
    [GraphQLDescription("Downstream: POST /v2/b-stores/parent-portal/{portalId}/users/{userId}/setup")]
    [RequireBStoreAdmin]
    public async Task<CreateStoreResult?> BStoreCreate(
        int portalId,
        int userId,
        CreateBStoreInput input,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreCreate: portalId={PortalId} userId={UserId}", portalId, userId);
        try
        {
            return await bStore.CreateStoreAsync(portalId, userId, input, ct);
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreCreate failed: portalId={PortalId}", portalId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    // ── POST /v2/b-stores/{sourcePortalId}/copy ───────────────────────────────

    /// <summary>Duplicates an existing B-store.</summary>
    [GraphQLDescription("Downstream: POST /v2/b-stores/{sourcePortalId}/copy")]
    [RequireBStoreAdmin]
    public async Task<bool> BStoreCopy(
        int sourcePortalId,
        int userId,
        DuplicateBStoreInput input,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreCopy: sourcePortalId={SourcePortalId}", sourcePortalId);
        try
        {
            var result = await bStore.DuplicateStoreAsync(sourcePortalId, userId, input, ct);
            if (result)
                await cache.RemoveAsync(CacheKeys.ForBStore(sourcePortalId), ct);
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreCopy failed: sourcePortalId={SourcePortalId}", sourcePortalId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    // ── POST /v2/b-stores/{storeId}/users/{userId}/set-activation ────────────

    /// <summary>Activates or deactivates a B-store.</summary>
    [GraphQLDescription("Downstream: POST /v2/b-stores/{storeId}/users/{userId}/set-activation")]
    [RequireBStoreAdmin]
    public async Task<bool> BStoreSetActivation(
        int storeId,
        int userId,
        bool active,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreSetActivation: storeId={StoreId} active={Active}", storeId, active);
        try
        {
            var result = await bStore.SetActivationAsync(storeId, userId, active, ct);
            if (result)
            {
                await cache.RemoveAsync(CacheKeys.ForBStore(storeId), ct);
                await TryPublishEventAsync(
                    BStoreGraphQLEventRoutingKeys.BStoreActivationChanged,
                    new { storeId, userId, active, at = DateTimeOffset.UtcNow },
                    ct);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreSetActivation failed: storeId={StoreId}", storeId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    // ── PUT /v2/b-stores/{storeId} ────────────────────────────────────────────

    /// <summary>Updates core settings for a B-store.</summary>
    [GraphQLDescription("Downstream: PUT /v2/b-stores/{storeId}")]
    [RequireBStoreAdmin]
    public async Task<bool> BStoreUpdate(
        int storeId,
        int userId,
        UpdateBStoreSettingsInput input,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreUpdate: storeId={StoreId}", storeId);
        try
        {
            var result = await bStore.UpdateStoreAsync(storeId, userId, input, ct);
            if (result)
            {
                await cache.RemoveAsync(CacheKeys.ForBStore(storeId), ct);
                await TryPublishEventAsync(
                    BStoreGraphQLEventRoutingKeys.BStoreSettingsUpdated,
                    new { storeId, userId, at = DateTimeOffset.UtcNow },
                    ct);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreUpdate failed: storeId={StoreId}", storeId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    // ── PUT /v2/b-stores/{storeId}/theme ─────────────────────────────────────

    /// <summary>Updates theme/branding settings for a B-store.</summary>
    [GraphQLDescription("Downstream: PUT /v2/b-stores/{storeId}/theme")]
    [RequireBStoreAdmin]
    public async Task<bool> BStoreThemeUpdate(
        int storeId,
        int userId,
        UpdateBStoreDesignInput input,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreThemeUpdate: storeId={StoreId}", storeId);
        try
        {
            var result = await bStore.UpdateStoreDesignAsync(storeId, userId, input, ct);
            if (result)
            {
                await cache.RemoveAsync(CacheKeys.BStoreTheme(storeId), ct);
                await TryPublishEventAsync(
                    BStoreGraphQLEventRoutingKeys.BStoreThemeUpdated,
                    new { storeId, userId, at = DateTimeOffset.UtcNow },
                    ct);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreThemeUpdate failed: storeId={StoreId}", storeId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    // ── POST /fileupload/post ──────────────────────────────────────────────────

    /// <summary>Uploads a file to media storage.</summary>
    [GraphQLDescription("Downstream: POST /FileUpload/post (also /fileupload/post on some hosts)")]
    [RequireBStoreAdmin]
    public async Task<FileUploadResult?> BStoreUploadFile(
        IFile file,
        int mediaId,
        string? fileType,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreUploadFile: fileName={FileName}", file.Name);
        try
        {
            return await bStore.UploadFileAsync(file, mediaId, fileType, ct);
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreUploadFile failed: fileName={FileName}", file.Name);
            throw ErrorMapper.ToGraphQL(ex, ErrorCodes.Upload);
        }
    }

    // ── POST /fileupload/remove ───────────────────────────────────────────────

    /// <summary>Deletes uploaded media by comma-separated media IDs.</summary>
    [GraphQLDescription("Downstream: POST /FileUpload/remove")]
    [RequireBStoreAdmin]
    public async Task<bool> BStoreRemoveUploadedFile(
        string mediaIds,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreRemoveUploadedFile: mediaIds={MediaIds}", mediaIds);
        try
        {
            return await bStore.DeleteFileAsync(mediaIds, ct);
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreRemoveUploadedFile failed");
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    private async Task TryPublishEventAsync(string routingKey, object payload, CancellationToken ct)
    {
        try
        {
            await events.PublishAsync(routingKey, payload, headers: null, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Integration event publish failed after successful mutation | {RoutingKey}", routingKey);
        }
    }
}
