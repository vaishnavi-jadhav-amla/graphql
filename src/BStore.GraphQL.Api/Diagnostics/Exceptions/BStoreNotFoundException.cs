namespace BStore.GraphQL.Api.Diagnostics.Exceptions;

/// <summary>
/// Thrown when a B-store resource cannot be found or is inaccessible to the caller.
/// Mapped to <c>BSTORE_NOT_FOUND</c> in the error filter.
/// </summary>
public sealed class BStoreNotFoundException : Exception
{
    public int? BStoreId { get; }

    public BStoreNotFoundException(int bStoreId)
        : base($"B-store {bStoreId} was not found or is not accessible.")
    {
        BStoreId = bStoreId;
    }

    public BStoreNotFoundException(string message) : base(message) { }

    public BStoreNotFoundException(string message, Exception inner) : base(message, inner) { }
}
