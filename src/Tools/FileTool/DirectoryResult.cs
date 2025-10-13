using System.ComponentModel;

namespace FileTool;

public class DirectoryResult
{
    [Description("Indicates whether the operation completed successfully")]
    public bool Success { get; set; }

    [Description("Error message if operation failed, null if successful")]
    public string? Error { get; set; }

    [Description("Path of the directory that was operated on")]
    public string Path { get; set; } = string.Empty;

    [Description("List of files and directories found in the directory")]
    public List<FileInfo> Items { get; set; } = new List<FileInfo>();
}
