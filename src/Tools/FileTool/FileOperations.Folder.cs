using System.ComponentModel;
using Zio;

namespace FileTool;

internal partial class FileOperations
{
    [DisplayName("folder_operation")]
    [Description("Performs various folder operations including create, delete, list, and search")]
    public object FolderOperation(
        [Description("Type of operation to perform: create, delete, list, or search")] string operationType,
        [Description("Target directory path")] string path,
        [Description("Pattern to use when searching for files (only required for search operation)")] string? searchPattern = null,
        [Description("Whether to perform the operation recursively on subdirectories")] bool recursive = true)
    {
        try
        {
            switch (operationType.ToLower())
            {
                case FolderOperationTypes.Create:
                    return CreateDirectory(path);
                case FolderOperationTypes.Delete:
                    return DeleteDirectory(path, recursive);
                case FolderOperationTypes.List:
                    return ListDirectory(path);
                case FolderOperationTypes.Search:
                    if (searchPattern == null)
                    {
                        return new DirectoryResult
                        {
                            Success = false,
                            Error = Properties.Resources.Error_SearchPatternRequired,
                            Path = path
                        };
                    }
                    return SearchFiles(path, searchPattern, recursive);
                default:
                    return new DirectoryResult
                    {
                        Success = false,
                        Error = string.Format(Properties.Resources.Error_UnknownOperationType, operationType),
                        Path = path
                    };
            }
        }
        catch (Exception ex)
        {
            if (operationType.ToLower() == FolderOperationTypes.Create || operationType.ToLower() == FolderOperationTypes.Delete)
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_FolderOperation, ex.Message),
                    Path = path
                };
            }
            else
            {
                return new DirectoryResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_FolderOperation, ex.Message),
                    Path = path
                };
            }
        }
    }

    private FileResult CreateDirectory(string path)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (_fileSystem.DirectoryExists(upath))
            {
                return new FileResult
                {
                    Success = true,
                    Path = upath.FullName
                };
            }
            
            _fileSystem.CreateDirectory(upath);
            return new FileResult
            {
                Success = true,
                Path = upath.FullName
            };
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_CreatingDirectory, ex.Message),
                Path = path
            };
        }
    }

    private FileResult DeleteDirectory(string path, bool recursive = true)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (!_fileSystem.DirectoryExists(upath))
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_DirectoryNotFound, upath),
                    Path = upath.FullName
                };
            }
            
            _fileSystem.DeleteDirectory(upath, recursive);
            return new FileResult
            {
                Success = true,
                Path = upath.FullName
            };
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_DeletingDirectory, ex.Message),
                Path = path
            };
        }
    }

    private DirectoryResult ListDirectory(string path)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (!_fileSystem.DirectoryExists(upath))
            {
                return new DirectoryResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_DirectoryNotFound, upath),
                    Path = upath.FullName
                };
            }
            
            var items = new List<FileInfo>();
            
            // Get directories
            foreach (var dir in _fileSystem.EnumerateDirectories(upath))
            {
                var dirName = dir.GetName();
                var fullPath = dir.FullName;
                var lastWriteTime = _fileSystem.GetLastWriteTime(dir);
                
                items.Add(new FileInfo
                {
                    Name = dirName,
                    FullPath = fullPath,
                    Size = 0,
                    LastModified = lastWriteTime,
                    IsDirectory = true,
                    Extension = string.Empty
                });
            }
            
            // Get files
            foreach (var file in _fileSystem.EnumerateFiles(upath))
            {
                var fileName = file.GetName();
                var fullPath = file.FullName;
                var size = _fileSystem.GetFileLength(file);
                var lastWriteTime = _fileSystem.GetLastWriteTime(file);
                var extension = System.IO.Path.GetExtension(fileName);
                
                items.Add(new FileInfo
                {
                    Name = fileName,
                    FullPath = fullPath,
                    Size = size,
                    LastModified = lastWriteTime,
                    IsDirectory = false,
                    Extension = extension
                });
            }
            
            return new DirectoryResult
            {
                Success = true,
                Path = upath.FullName,
                Items = items
            };
        }
        catch (Exception ex)
        {
            return new DirectoryResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_ListingDirectory, ex.Message),
                Path = path
            };
        }
    }

    private DirectoryResult SearchFiles(string path, string searchPattern, bool recursive = true)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (!_fileSystem.DirectoryExists(upath))
            {
                return new DirectoryResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_DirectoryNotFound, upath),
                    Path = upath.FullName
                };
            }
            
            var items = new List<FileInfo>();
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            
            foreach (var file in _fileSystem.EnumerateFiles(upath, searchPattern, searchOption))
            {
                var fileName = file.GetName();
                var fullPath = file.FullName;
                var size = _fileSystem.GetFileLength(file);
                var lastWriteTime = _fileSystem.GetLastWriteTime(file);
                var extension = System.IO.Path.GetExtension(fileName);
                
                items.Add(new FileInfo
                {
                    Name = fileName,
                    FullPath = fullPath,
                    Size = size,
                    LastModified = lastWriteTime,
                    IsDirectory = false,
                    Extension = extension
                });
            }
            
            return new DirectoryResult
            {
                Success = true,
                Path = upath.FullName,
                Items = items
            };
        }
        catch (Exception ex)
        {
            return new DirectoryResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_SearchingFiles, ex.Message),
                Path = path
            };
        }
    }
} 