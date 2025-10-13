using System;

namespace DaoStudio.Plugins.KVStore;


/// <summary>
/// Configuration class for KeyValueStore plugin
/// This is a minimal configuration as specified in the requirements
/// </summary>
public class KeyValueStoreConfig
{
    /// <summary>
    /// Current version of the config schema
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Controls whether key comparisons are case sensitive
    /// </summary>
    public bool IsCaseSensitive { get; set; } = true;

    /// <summary>
    /// Instance name for the plugin. When not null, functions will be prefixed with this name followed by an underscore.
    /// </summary>
    public string? InstanceName { get; set; } = null;
}
