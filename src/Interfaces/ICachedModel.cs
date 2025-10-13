namespace DaoStudio.Interfaces;


/// <summary>
/// Core cached model interface for UI layer
/// </summary>
public interface ICachedModel
{
    /// <summary>
    /// Unique identifier for the cached model
    /// </summary>
    long Id { get; set; }

    /// <summary>
    /// API provider ID this model belongs to
    /// </summary>
    long ApiProviderId { get; set; }

    /// <summary>
    /// Display name of the model
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Model identifier used by the provider
    /// </summary>
    string ModelId { get; set; }

    /// <summary>
    /// Type of the provider
    /// </summary>
    ProviderType ProviderType { get; set; }

    /// <summary>
    /// Model catalog or category
    /// </summary>
    string Catalog { get; set; }
}