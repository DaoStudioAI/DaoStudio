using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System.Text.Json;
using Zio;
using Zio.FileSystems;

namespace FileTool;

// File and folder operation types as string constants
internal static class FileOperationTypes
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Delete = "delete";
    public const string Copy = "copy";
    public const string Move = "move";
    public const string Exists = "exists";
    public const string GetInfo = "get_info";
    public const string ApplyDiff = "apply_diff";
}

internal static class FolderOperationTypes
{
    public const string Create = "create";
    public const string Delete = "delete";
    public const string List = "list";
    public const string Search = "search";
}

// Consolidated class that implements both file operations and plugin functionality
internal partial class FileOperations : BasePlugin<FileToolConfig>
{
    private readonly IHostSession? _hostSession;
    private readonly IFileSystem _fileSystem;

    // Constructor for plugin functionality
    public FileOperations(PlugToolInfo plugInstanceInfo) : base(plugInstanceInfo)
    {
        var physicalFS = new PhysicalFileSystem();
        _fileSystem = new SubFileSystem(physicalFS, physicalFS.ConvertPathFromInternal(_config.RootDirectory));
    }

    // Constructor for direct use (backwards compatibility)
    public FileOperations(FileToolConfig config, IHostSession hostSession) : base(new PlugToolInfo())
    {
        _hostSession = hostSession ?? throw new ArgumentNullException(nameof(hostSession));
        
        // Override the config with the provided one
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        // Create a SubFileSystem rooted at the configured directory
        var physicalFS = new PhysicalFileSystem();
        _fileSystem = new SubFileSystem(physicalFS, physicalFS.ConvertPathFromInternal(_config.RootDirectory));
    }

    protected override FileToolConfig CreateDefaultConfiguration()
    {
        return new FileToolConfig
        {
            RootDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
    }

    // IPlugin interface implementation
    protected override async Task RegisterToolFunctionsAsync(List<FunctionWithDescription> toolcallFunctions,
        IHostPerson? person, IHostSession? hostSession)
    {
        // Update the host session reference (for plugin use)
        if (_hostSession == null)
        {
            // Use reflection to set the readonly field for plugin usage
            var hostSessionField = typeof(FileOperations).GetField("_hostSession", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            hostSessionField?.SetValue(this, hostSession);
        }

        // Use the extension method to create functions from the file tool methods
        var toolFunctions = IPluginExtensions.CreateFunctionsFromToolMethods(this, "Files");
        toolcallFunctions.AddRange(toolFunctions);
    }

    private UPath ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            // For rooted paths, we need to make them relative to the root directory
            var physicalFS = new PhysicalFileSystem();
            var rootPath = physicalFS.ConvertPathFromInternal(_config.RootDirectory);
            var fullPath = physicalFS.ConvertPathFromInternal(path);
            
            // Check if the path is within the root directory
            if (fullPath.IsInDirectory(rootPath, true))
            {
                return UPath.Root / fullPath.ToString().Substring(rootPath.ToString().Length).TrimStart('/');
            }
            throw new UnauthorizedAccessException($"Access to path '{path}' is restricted to the configured root directory");
        }
        
        // For relative paths, just convert them directly since the _fileSystem is already rooted
        return UPath.Root / path.Replace('\\', '/');
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources
            _fileSystem?.Dispose();
        }
        
        base.Dispose(disposing);
    }
}