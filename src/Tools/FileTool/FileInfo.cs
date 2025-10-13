using System.ComponentModel;

namespace FileTool;

public class FileInfo
{
    [Description("Name of the file or directory")]
    public string Name { get; set; } = string.Empty;

    [Description("Full path of the file or directory")]
    public string FullPath { get; set; } = string.Empty;

    [Description("Size of the file in bytes, 0 for directories")]
    public long Size { get; set; }

    [Description("Last modification time of the file or directory")]
    public DateTime LastModified { get; set; }

    [Description("True if this is a directory, false if it's a file")]
    public bool IsDirectory { get; set; }

    [Description("File extension if this is a file, empty for directories")]
    public string Extension { get; set; } = string.Empty;
}
