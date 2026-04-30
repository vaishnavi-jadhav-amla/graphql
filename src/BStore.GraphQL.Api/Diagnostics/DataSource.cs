namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Canonical data-source labels for ADR-022 attribution.
/// Use these constants whenever recording <see cref="IRequestDebugContext.RecordDataSource"/>.
/// </summary>
public static class DataSource
{
    public const string ZnodeEntities        = "Sql:Znode_Entities";
    public const string ZnodePublishEntities = "Sql:ZnodePublish_Entities";
    public const string CacheL1              = "Cache:L1-Memory";
    public const string CacheL2              = "Cache:L2-Distributed";
    public const string AzureCognitiveSearch = "AzureCognitiveSearch";
    public const string AuthApi              = "Http:AuthApi";
    public const string RabbitMq             = "AMQP:RabbitMQ";
    public const string Cdn                  = "Http:CDN";
}
