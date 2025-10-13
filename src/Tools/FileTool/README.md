# FileTool Plugin

The FileTool plugin provides file system operations for LLM agents, enabling them to read, write, and manipulate files and directories on the host system.

## Features

- Read and write text files
- Create, delete, copy, and move files
- List directory contents
- Search for files using wildcard patterns
- Get file information
- Manipulate directories (create, delete)
- Apply targeted diffs to files

## Usage

The FileTool plugin exposes various functions that can be called by LLM agents. Each function returns a structured result indicating success or failure, along with relevant data.

### Available Functions

| Function | Description |
|----------|-------------|
| `file_operation` | Consolidated function for all file operations |
| `folder_operation` | Consolidated function for all folder operations |

### Operation Types

#### File Operations

The `file_operation` function supports the following operation types (as strings):

- `read`: Reads the content of a text file
- `write`: Writes or appends text content to a file
- `delete`: Deletes a file
- `copy`: Copies a file to a new location
- `move`: Moves a file to a new location
- `exists`: Checks if a file exists
- `get_info`: Gets information about a file
- `apply_diff`: Makes precise changes to files using a sophisticated diff format

#### Folder Operations

The `folder_operation` function supports the following operation types (as strings):

- `create`: Creates a directory
- `delete`: Deletes a directory
- `list`: Lists the contents of a directory
- `search`: Searches for files matching a pattern

### Path Resolution

The FileTool plugin resolves paths based on the configured default directory. If you provide a relative path, it will be resolved relative to the default directory. If you provide an absolute path, it will be used as-is.

## Example

Here's an example of how an LLM agent might use the FileTool:

```csharp
// Read a file
var result = fileOperations.FileOperation("read", "example.txt");
if (result.Success)
{
    // Process file content
    var content = result.Content;
}
else
{
    // Handle error
    var errorMessage = result.Error;
}

// Write to a file
fileOperations.FileOperation("write", "output.txt", content: "Hello, world!");

// Append to a file
fileOperations.FileOperation("write", "output.txt", content: "Additional content.", append: true);

// Apply a diff to make targeted changes
var diff = @"<<<<<<< SEARCH
:start_line:10
:end_line:12
-------
    // Old calculation logic
    const result = value * 0.9;
    return result;
=======
    // Updated calculation logic with logging
    console.log(`Calculating for value: ${value}`);
    const result = value * 0.95; // Adjusted factor
    return result;
>>>>>>> REPLACE";

fileOperations.FileOperation("apply_diff", "calculate.js", diff: diff);

// List directory contents
var dirResult = fileOperations.FolderOperation("list", "Documents");
if (dirResult.Success)
{
    foreach (var item in dirResult.Items)
    {
        // Process each file/directory
        if (item.IsDirectory)
        {
            // Handle directory
        }
        else
        {
            // Handle file
            var fileName = item.Name;
            var size = item.Size;
        }
    }
}
```

## Security Considerations

The FileTool plugin provides direct access to the file system, which can pose security risks if not used properly. Consider the following when using this plugin:

- Avoid allowing unrestricted access to system directories
- Validate and sanitize path inputs from LLM agents
- Consider implementing path restrictions or sandboxing
- Use with trusted LLM agents only

## Configuration

The FileTool can be configured with a default directory to use as the base for relative paths. This is set during plugin creation and can be modified through the configuration UI. 