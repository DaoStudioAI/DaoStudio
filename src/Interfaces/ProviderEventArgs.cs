using System;

namespace DaoStudio.Interfaces;

/// <summary>
/// Defines the type of provider operation performed
/// </summary>
public enum ProviderOperationType
{
    Created,
    Updated,
    Deleted
}

/// <summary>
/// Event arguments for provider operation events
/// </summary>
public class ProviderOperationEventArgs : EventArgs
{
    public ProviderOperationType OperationType { get; set; }
    public IApiProvider? Provider { get; set; }
    public long? ProviderId { get; set; }

    /// <summary>
    /// Constructor for Create/Update operations
    /// </summary>
    /// <param name="operationType">The type of operation</param>
    /// <param name="provider">The provider involved in the operation</param>
    public ProviderOperationEventArgs(ProviderOperationType operationType, IApiProvider provider)
    {
        OperationType = operationType;
        Provider = provider;
        ProviderId = provider.Id;
    }

    /// <summary>
    /// Constructor for Delete operations
    /// </summary>
    /// <param name="providerId">The ID of the deleted provider</param>
    public ProviderOperationEventArgs(long providerId)
    {
        OperationType = ProviderOperationType.Deleted;
        ProviderId = providerId;
        Provider = null;
    }
}
