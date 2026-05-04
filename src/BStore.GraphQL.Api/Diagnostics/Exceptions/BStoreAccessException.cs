namespace BStore.GraphQL.Api.Diagnostics.Exceptions;

/// <summary>
/// Thrown when a user attempts to access a B-store they do not own or manage.
/// IDOR guard — prevents horizontal privilege escalation across B-stores.
/// </summary>
public sealed class BStoreAccessException : Exception
{
    public int UserId { get; }
    public int BStoreId { get; }

    public BStoreAccessException(int userId, int bStoreId)
        : base($"User {userId} does not have access to B-store {bStoreId}.")
    {
        UserId = userId;
        BStoreId = bStoreId;
    }

    public BStoreAccessException(string message) : base(message) { }

    public BStoreAccessException(string message, Exception inner) : base(message, inner) { }
}
