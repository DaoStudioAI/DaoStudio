using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Zio;

namespace FileTool;

internal partial class FileOperations
{
    [DisplayName("file_operation")]
    [Description("Performs various file operations including read, write, delete, copy, move, exists, get info, and apply diff")]
    public FileResult FileOperation(
        [Description("Type of operation to perform: read, write, delete, copy, move, exists, get_info, apply_diff")] string operationType,
        [Description("Path to the file to operate on")] string path,
        [Description("Content to write or diff to apply (required for write and apply_diff operations)")] string? content = null,
        [Description("Destination path for copy and move operations")] string? destinationPath = null,
        [Description("Start line for apply_diff operation (1-based indexing)")] int? startLine = null,
        [Description("End line for apply_diff operation (1-based indexing)")] int? endLine = null,
        [Description("Whether to append content to existing file instead of overwriting it")] bool append = false,
        [Description("Whether to overwrite destination file if it exists for copy and move operations")] bool overwrite = true)
    {
        // Validate parameters and get normalized operation type
        var validationResult = ValidateFileOperationParameters(operationType, path, content, destinationPath, startLine, endLine);
        if (!validationResult.IsValid)
        {
            return validationResult.ErrorResult;
        }
        
        string lowerOp = validationResult.NormalizedOperationType;

        try
        {
            switch (lowerOp)
            {
                case FileOperationTypes.Read:
                    return ReadTextFile(path);
                case FileOperationTypes.Write:
                    return WriteTextFile(path, content!, append);
                case FileOperationTypes.Delete:
                    return DeleteFile(path);
                case FileOperationTypes.Copy:
                    return CopyFile(path, destinationPath!, overwrite);
                case FileOperationTypes.Move:
                    return MoveFile(path, destinationPath!, overwrite);
                case FileOperationTypes.Exists:
                    return FileExists(path);
                case FileOperationTypes.GetInfo:
                    return GetFileInfo(path);
                case FileOperationTypes.ApplyDiff:
                    return ApplyDiff(path, content!, startLine, endLine);
                default:
                    return new FileResult
                    {
                        Success = false,
                        Error = string.Format(Properties.Resources.Error_UnknownOperationType, operationType),
                        Path = path
                    };
            }
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_FileOperation, ex.Message),
                Path = path
            };
        }
    }

    private FileResult ReadTextFile([Description("Path to the file to read")] string path)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (!_fileSystem.FileExists(upath))
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_FileNotFound, upath),
                    Path = upath.FullName
                };
            }
            
            var content = _fileSystem.ReadAllText(upath);
            return new FileResult
            {
                Success = true,
                Content = content,
                Path = upath.FullName
            };
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_ReadingFile, ex.Message),
                Path = path
            };
        }
    }

    private FileResult WriteTextFile([Description("Path to the file to write")] string path, [Description("Content to write to the file")] string content, [Description("Whether to append content to existing file instead of overwriting it")] bool append = false)
    {
        try
        {
            var upath = ResolvePath(path);
            
            // Create directory if it doesn't exist
            var directory = upath.GetDirectory();
            if (!_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }
            
            if (append)
            {
                _fileSystem.AppendAllText(upath, content);
            }
            else
            {
                _fileSystem.WriteAllText(upath, content);
            }
            
            return new FileResult
            {
                Success = true,
                Path = upath.FullName
            };
        }
        catch (Exception ex)
        {
            string operation = append ? Properties.Resources.Operation_Appending : Properties.Resources.Operation_Writing;
            return new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_WritingFile, operation, ex.Message),
                Path = path
            };
        }
    }

    private FileResult DeleteFile([Description("Path to the file to delete")] string path)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (!_fileSystem.FileExists(upath))
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_FileNotFound, upath),
                    Path = upath.FullName
                };
            }
            
            _fileSystem.DeleteFile(upath);
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
                Error = string.Format(Properties.Resources.Error_DeletingFile, ex.Message),
                Path = path
            };
        }
    }

    private FileResult CopyFile([Description("Path to the source file to copy")] string sourcePath, [Description("Path to the destination file")] string destinationPath, [Description("Whether to overwrite destination file if it exists")] bool overwrite = true)
    {
        try
        {
            var sourceUPath = ResolvePath(sourcePath);
            var destinationUPath = ResolvePath(destinationPath);
            
            if (!_fileSystem.FileExists(sourceUPath))
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_SourceFileNotFound, sourceUPath),
                    Path = sourceUPath.FullName
                };
            }
            
            // Create directory if it doesn't exist
            var directory = destinationUPath.GetDirectory();
            if (!_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }
            
            _fileSystem.CopyFile(sourceUPath, destinationUPath, overwrite);
            return new FileResult
            {
                Success = true,
                Path = destinationUPath.FullName
            };
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_CopyingFile, ex.Message),
                Path = destinationPath
            };
        }
    }

    private (bool IsValid, FileResult ErrorResult, string NormalizedOperationType) ValidateFileOperationParameters(
        string operationType, string path, string? content, string? destinationPath, int? startLine, int? endLine)
    {
        // Check operation type
        if (string.IsNullOrWhiteSpace(operationType))
        {
            return (false, new FileResult
            {
                Success = false,
                Error = Properties.Resources.Error_OperationTypeNull,
                Path = path
            }, string.Empty);
        }

        // Check path
        if (string.IsNullOrWhiteSpace(path))
        {
            return (false, new FileResult
            {
                Success = false,
                Error = Properties.Resources.Error_FilePathNull,
                Path = path ?? string.Empty
            }, string.Empty);
        }
        
        string lowerOp = operationType.ToLower();
        
        // Validate operation-specific parameters
        if ((lowerOp == FileOperationTypes.Write || lowerOp == FileOperationTypes.ApplyDiff) && content == null)
        {
            return (false, new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_ContentRequired, lowerOp),
                Path = path
            }, lowerOp);
        }
        
        if ((lowerOp == FileOperationTypes.Copy || lowerOp == FileOperationTypes.Move) && string.IsNullOrWhiteSpace(destinationPath))
        {
            return (false, new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_DestinationPathRequired, lowerOp),
                Path = path
            }, lowerOp);
        }
        
        if (lowerOp == FileOperationTypes.ApplyDiff && (!startLine.HasValue || !endLine.HasValue))
        {
            return (false, new FileResult
            {
                Success = false,
                Error = Properties.Resources.Error_StartEndLineRequired,
                Path = path
            }, lowerOp);
        }
        
        // All validations passed
        return (true, null!, lowerOp);
    }

    private FileResult MoveFile([Description("Path to the source file to move")] string sourcePath, [Description("Path to the destination file")] string destinationPath, [Description("Whether to overwrite destination file if it exists")] bool overwrite = true)
    {
        try
        {
            var sourceUPath = ResolvePath(sourcePath);
            var destinationUPath = ResolvePath(destinationPath);
            
            if (!_fileSystem.FileExists(sourceUPath))
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_SourceFileNotFound, sourceUPath),
                    Path = sourceUPath.FullName
                };
            }
            
            // Create directory if it doesn't exist
            var directory = destinationUPath.GetDirectory();
            if (!_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }
            
            // If overwrite is true and destination exists, delete it first
            if (overwrite && _fileSystem.FileExists(destinationUPath))
            {
                _fileSystem.DeleteFile(destinationUPath);
            }
            
            _fileSystem.MoveFile(sourceUPath, destinationUPath);
            return new FileResult
            {
                Success = true,
                Path = destinationUPath.FullName
            };
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = string.Format(Properties.Resources.Error_MovingFile, ex.Message),
                Path = destinationPath
            };
        }
    }

    private FileResult FileExists([Description("Path to the file to check existence")] string path)
    {
        try
        {
            var upath = ResolvePath(path);
            bool exists = _fileSystem.FileExists(upath);
            
            return new FileResult
            {
                Success = true,
                Content = exists.ToString().ToLower(),
                Path = upath.FullName
            };
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = $"Error checking file existence: {ex.Message}",
                Path = path
            };
        }
    }

    private FileResult GetFileInfo([Description("Path to the file to get information about")] string path)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (!_fileSystem.FileExists(upath))
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_FileNotFound, upath),
                    Path = upath.FullName
                };
            }
            
            var name = upath.GetName();
            var fullPath = upath.FullName;
            var size = _fileSystem.GetFileLength(upath);
            var lastWriteTime = _fileSystem.GetLastWriteTime(upath);
            var extension = System.IO.Path.GetExtension(name);
            
            var info = new FileInfo
            {
                Name = name,
                FullPath = fullPath,
                Size = size,
                LastModified = lastWriteTime,
                IsDirectory = false,
                Extension = extension
            };
            
            return new FileResult
            {
                Success = true,
                Content = JsonSerializer.Serialize(info),
                Path = upath.FullName
            };
        }
        catch (Exception ex)
        {
            return new FileResult
            {
                Success = false,
                Error = $"Error getting file info: {ex.Message}",
                Path = path
            };
        }
    }

    private FileResult ApplyDiff([Description("Path to the file to apply diff to")] string path, [Description("Diff content to apply to the file")] string diff, [Description("Start line for diff application (1-based indexing)")] int? startLine = null, [Description("End line for diff application (1-based indexing)")] int? endLine = null)
    {
        try
        {
            var upath = ResolvePath(path);
            
            if (!_fileSystem.FileExists(upath))
            {
                return new FileResult
                {
                    Success = false,
                    Error = string.Format(Properties.Resources.Error_FileNotFound, upath),
                    Path = upath.FullName
                };
            }
            
            // Read the current file content
            var originalContent = _fileSystem.ReadAllText(upath);
            string updatedContent;
            
            // Apply MultiSearchReplaceDiffStrategy
            try
            {
                updatedContent = ApplyMultiSearchReplaceDiff(originalContent, diff, startLine, endLine);
            }
            catch (Exception ex)
            {
                return new FileResult
                {
                    Success = false,
                    Error = $"Error applying diff: {ex.Message}",
                    Path = upath.FullName
                };
            }
            
            // Write the updated content back to the file
            _fileSystem.WriteAllText(upath, updatedContent);
            
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
                Error = $"Error applying diff: {ex.Message}",
                Path = path
            };
        }
    }
    
    private string ApplyMultiSearchReplaceDiff([Description("Original content of the file")] string originalContent, [Description("Diff content to apply")] string diff, [Description("Hint for start line (1-based indexing)")] int? hintStartLine, [Description("Hint for end line (1-based indexing)")] int? hintEndLine)
    {
        // Split content into lines for line-based operations
        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        // Constants for diff parsing
        const string searchStartDelimiter = "<<<<<<< SEARCH";
        const string separatorDelimiter = "-------";
        const string replaceStartDelimiter = "=======";
        const string replaceEndDelimiter = ">>>>>>> REPLACE";
        
        // Regex patterns for extracting line information
        var startLinePattern = new Regex(@":start_line:(\d+)");
        var endLinePattern = new Regex(@":end_line:(\d+)");
        
        // Split the diff into separate blocks
        var diffBlocks = diff.Split(new[] { searchStartDelimiter }, StringSplitOptions.RemoveEmptyEntries);
        
        // Skip the first element if it's empty
        int startIndex = diffBlocks.Length > 0 && string.IsNullOrWhiteSpace(diffBlocks[0]) ? 1 : 0;
        
        // Parse and apply each diff block
        for (int i = startIndex; i < diffBlocks.Length; i++)
        {
            var block = diffBlocks[i];
            
            // Skip if the block is empty
            if (string.IsNullOrWhiteSpace(block))
                continue;
            
            // Split the block into search and replace sections
            var sections = block.Split(new[] { separatorDelimiter }, StringSplitOptions.None);
            if (sections.Length < 2)
                throw new FormatException("Invalid diff block format: Missing separator");
            
            var searchMetadata = sections[0];
            var searchAndReplaceContent = sections[1];
            
            // Extract line numbers from metadata
            int specificStartLine = -1;
            int specificEndLine = -1;
            
            var startLineMatch = startLinePattern.Match(searchMetadata);
            if (startLineMatch.Success)
            {
                specificStartLine = int.Parse(startLineMatch.Groups[1].Value);
            }
            else if (hintStartLine.HasValue)
            {
                specificStartLine = hintStartLine.Value;
            }
            else
            {
                throw new FormatException("Start line not specified in diff block and no hint provided");
            }
            
            var endLineMatch = endLinePattern.Match(searchMetadata);
            if (endLineMatch.Success)
            {
                specificEndLine = int.Parse(endLineMatch.Groups[1].Value);
            }
            else if (hintEndLine.HasValue)
            {
                specificEndLine = hintEndLine.Value;
            }
            else
            {
                throw new FormatException("End line not specified in diff block and no hint provided");
            }
            
            // Adjust for 0-based indexing
            specificStartLine = Math.Max(1, specificStartLine) - 1;
            specificEndLine = Math.Min(lines.Length, specificEndLine) - 1;
            
            if (specificStartLine < 0 || specificEndLine >= lines.Length || specificStartLine > specificEndLine)
            {
                throw new ArgumentOutOfRangeException("Line numbers are out of range");
            }
            
            // Split the content to get search and replace sections
            var contentSections = searchAndReplaceContent.Split(new[] { replaceStartDelimiter }, StringSplitOptions.None);
            if (contentSections.Length < 2)
                throw new FormatException("Invalid diff block format: Missing replace delimiter");
            
            var searchContent = contentSections[0].Trim();
            
            // Extract the replace content by finding the end delimiter
            var replaceWithEndDelimiter = contentSections[1];
            var replaceEndIndex = replaceWithEndDelimiter.LastIndexOf(replaceEndDelimiter);
            
            if (replaceEndIndex < 0)
                throw new FormatException("Invalid diff block format: Missing replace end delimiter");
            
            var replaceContent = replaceWithEndDelimiter.Substring(0, replaceEndIndex).Trim();
            
            // Extract the target content from the file
            var targetContentBuilder = new System.Text.StringBuilder();
            for (int lineIdx = specificStartLine; lineIdx <= specificEndLine; lineIdx++)
            {
                targetContentBuilder.AppendLine(lines[lineIdx]);
            }
            
            var targetContent = targetContentBuilder.ToString().TrimEnd();
            
            // Verify the search content matches the target content
            if (targetContent != searchContent)
            {
                const int CONTEXT_LINES = 3;
                
                // Show context for debugging
                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine($"Expected to find at lines {specificStartLine + 1}-{specificEndLine + 1}:");
                contextBuilder.AppendLine("----------- SEARCH CONTENT -----------");
                contextBuilder.AppendLine(searchContent);
                contextBuilder.AppendLine("----------- ACTUAL CONTENT -----------");
                contextBuilder.AppendLine(targetContent);
                
                // Show a few lines before and after for context
                contextBuilder.AppendLine("----------- CONTEXT LINES -----------");
                int contextStart = Math.Max(0, specificStartLine - CONTEXT_LINES);
                int contextEnd = Math.Min(lines.Length - 1, specificEndLine + CONTEXT_LINES);
                
                for (int lineIdx = contextStart; lineIdx <= contextEnd; lineIdx++)
                {
                    string prefix = lineIdx == specificStartLine ? ">>> " : 
                                   lineIdx == specificEndLine ? "<<< " : "    ";
                    contextBuilder.AppendLine($"{prefix}[{lineIdx + 1}]: {lines[lineIdx]}");
                }
                
                throw new FormatException($"Content mismatch. {contextBuilder}");
            }
            
            // Replace the content
            var newLines = replaceContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // Create a new array with the replaced content
            var updatedLines = new List<string>(lines);
            updatedLines.RemoveRange(specificStartLine, specificEndLine - specificStartLine + 1);
            updatedLines.InsertRange(specificStartLine, newLines);
            
            // Update the lines array for the next iteration
            lines = updatedLines.ToArray();
        }
        
        // Rejoin all lines
        return string.Join(Environment.NewLine, lines);
    }
} 