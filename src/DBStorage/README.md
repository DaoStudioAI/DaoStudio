# DaoStudio.DBStorage

The application is a chat client for LLM. This project is the storage layer in 3-tier architecture.
A C# class library for saving and retrieving application settings using SQLite as storage.

## Features

- Store and retrieve application settings in SQLite database
- Manage LLM models, providers, tools, prompts, and sessions
- Simple API
- Thread-safe operations
- Support for dependency injection
- Database migration system using SQLite user_version
- Efficient numeric ID system with collision detection
- **Performance optimizations including connection pooling, WAL mode, and selective data loading**

## Performance Optimizations

This library includes several performance optimizations for high-load scenarios:

- **Connection Pooling**: All repositories use connection pooling with shared cache mode for better resource utilization
- **Write-Ahead Logging (WAL)**: Enabled for better concurrency - readers don't block writers
- **Strategic Database Indexing**: Composite indexes for common query patterns across all tables
- **Lazy Loading**: Optional binary data loading for Image, StateData, and BinaryContent fields
- **Batch Operations**: N+1 query prevention through batch loading methods
- **Foreign Key Enforcement**: Enabled across all repositories for data integrity

## Architecture

The repository implementation uses partial classes to organize related functionality into separate files for better maintainability. Each repository type (Settings, LLM Model, Tool, Provider, Prompt, Session) has a similar structure:

- **SqliteSettingsRepository**: 
  - `SqliteSettingsRepository.cs`: Base class and constructor
  - `SqliteSettingsRepository.Initialization.cs`: Database initialization
  - `SqliteSettingsRepository.Read.cs`: Read operations (Get, GetAll)
  - `SqliteSettingsRepository.Write.cs`: Write operations (Save, Delete)

- **LLM Repositories**:
  - `SqlitePersonRepository`, `SqliteLlmToolRepository`, `SqliteAPIProviderRepository`, `SqliteLlmPromptRepository`, `SqliteSessionRepository`, `SqliteCachedModelRepository`, `SqliteMessageRepository`: Each has corresponding Initialization, Read, and Write files.

This structure enhances code organization and maintainability by grouping related functionalities together.

The migration system components include:

- `IMigration.cs` - Interface for individual migrations
- `IMigrationManager.cs` - Interface for migration manager
- `SqliteMigrationManager.cs` - Implementation of migration manager
- `BaseMigration.cs` - Base abstract class for migrations
- Migration classes (e.g. `Migration_001_AddSettingsIndex.cs`)

## Database Schema

The library uses SQLite as its storage backend with several main tables:

### Settings Table
```sql
CREATE TABLE Settings (
    ApplicationName TEXT PRIMARY KEY,
    Version INTEGER NOT NULL,
    Properties TEXT NOT NULL,
    LastModified INTEGER NOT NULL,
    Theme INTEGER
);

CREATE INDEX idx_settings_last_modified ON Settings(LastModified);
```
- `ApplicationName`: Unique identifier for the application settings
- `Version`: Version number for the settings schema
- `Properties`: JSON-serialized dictionary of setting key-value pairs
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- `Theme`: Integer value representing theme (0=Light, 1=Dark, 2=System)
- Performance indexes on `LastModified` for temporal queries

### Applications Table
```sql
CREATE TABLE Applications (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    BriefDescription TEXT,
    Description TEXT,
    LastModified INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL
);

CREATE UNIQUE INDEX idx_applications_name ON Applications(Name);
CREATE INDEX idx_applications_last_modified ON Applications(LastModified);
```
- `Id`: Unique integer identifier for the application (Windows file-time friendly long)
- `Name`: Display name of the application (unique)
- `BriefDescription`: Optional short description
- `Description`: Optional detailed description
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- `CreatedAt`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of creation time
- Performance indexes on name uniqueness and `LastModified` for efficient lookups and temporal queries

### APIProviders Table
```sql
CREATE TABLE APIProviders (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    ApiEndpoint TEXT NOT NULL,
    ApiKey TEXT,
    Parameters TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL,
    LastModified INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    ProviderType INTEGER NOT NULL DEFAULT 4,
    Timeout INTEGER NOT NULL DEFAULT 30000,
    MaxConcurrency INTEGER NOT NULL DEFAULT 10
);

CREATE UNIQUE INDEX idx_api_providers_name ON APIProviders(Name);
CREATE INDEX idx_api_providers_enabled_type ON APIProviders(IsEnabled, ProviderType);
```
- `Id`: Unique integer identifier for the provider
- `Name`: Display name of the provider (e.g., "OpenAI", "Anthropic") - unique across all providers
- `ApiEndpoint`: API endpoint URL for the provider
- `ApiKey`: API key or authentication token (nullable)
- `Parameters`: JSON-serialized dictionary of provider parameters
- `IsEnabled`: Boolean flag (0 or 1) indicating if the provider is enabled
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- `CreatedAt`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of creation time
- `ProviderType`: Integer identifier for provider type (mapped to `DaoStudio.Interfaces.ProviderType`) with values: 0=Unknown, 1=OpenAI, 2=Anthropic, 3=Google, 4=Local, 5=OpenRouter, 6=Ollama, 7=LLama, 8=AWSBedrock (default value is 4=Local)
- `Timeout`: Request timeout in milliseconds (default: 30000)
- `MaxConcurrency`: Maximum number of concurrent requests allowed (default: 10)
- Performance indexes on name uniqueness and common filtering patterns (enabled + type)

### Persons Table
```sql
CREATE TABLE Persons (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL,
    ProviderName TEXT NOT NULL, 
    ModelId TEXT NOT NULL,
    PresencePenalty REAL,
    FrequencyPenalty REAL,
    TopP REAL,
    TopK INTEGER,
    Temperature REAL,
    Capability1 INTEGER,
    Capability2 INTEGER,
    Capability3 INTEGER,
    Image BLOB, 
    ToolNames TEXT NOT NULL, 
    Parameters TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL,
    LastModified INTEGER NOT NULL,
    DeveloperMessage TEXT,
    CreatedAt INTEGER NOT NULL,
    PersonType INTEGER NOT NULL DEFAULT 0,
    AppId INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_persons_provider_name ON Persons(ProviderName);
CREATE INDEX idx_persons_is_enabled ON Persons(IsEnabled);
CREATE UNIQUE INDEX idx_persons_name ON Persons(Name);
CREATE INDEX idx_persons_person_type ON Persons(PersonType);
CREATE INDEX idx_persons_app_id ON Persons(AppId);
CREATE INDEX idx_persons_enabled_provider ON Persons(IsEnabled, ProviderName);
```
- `Id`: Unique integer identifier for the model
- `Name`: Display name of the model (e.g., "GPT-4", "Claude-3") - unique across all models
- `Description`: Description of the person/model's capabilities and characteristics
- `ProviderName`: Name of the provider (e.g., "OpenAI", "Anthropic"). 
- `ModelId`: Full unique identifier string for the model (e.g., "openai/gpt-4o")
- `PresencePenalty`: Optional presence penalty parameter for controlling token repetition (nullable double)
- `FrequencyPenalty`: Optional frequency penalty parameter for controlling token repetition (nullable double)
- `TopP`: Optional top-p (nucleus) sampling parameter for response generation (nullable double)
- `TopK`: Optional top-k sampling parameter for response generation (nullable integer)
- `Temperature`: Optional temperature parameter for controlling randomness in responses (nullable double)
- `Capability1`: Optional capability flag 1 for model-specific features (nullable long)
- `Capability2`: Optional capability flag 2 for model-specific features (nullable long)
- `Capability3`: Optional capability flag 3 for model-specific features (nullable long)
- `Image`: Optional binary data (BLOB) for storing a model-specific image or logo (supports lazy loading)
- `ToolNames`: JSON-serialized list of strings representing names of compatible tools. 
- `Parameters`: JSON-serialized dictionary of model parameters (temperature, max tokens, etc.)
- `IsEnabled`: Boolean flag (0 or 1) indicating if the model is enabled
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- `DeveloperMessage`: Optional nullable message from developers about the model's usage, capabilities, or limitations
- `CreatedAt`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of creation time
- `PersonType`: Integer identifier for the type of person/model (default: 0)
- `AppId`: Integer identifier for the associated application (default: 0)
- Performance indexes on provider name, enabled status, person type, app ID, and composite enabled+provider for efficient filtering
- Supports batch operations via `GetPersonsByNamesAsync()` to prevent N+1 queries

### LlmTools Table
```sql
CREATE TABLE LlmTools (
    Id INTEGER PRIMARY KEY,
    StaticId TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL,
    ToolConfig TEXT NOT NULL,
    ToolType INTEGER NOT NULL,
    Parameters TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL,
    LastModified INTEGER NOT NULL,
    State INTEGER NOT NULL DEFAULT 0,
    StateData BLOB,
    DevMsg TEXT,
    CreatedAt INTEGER NOT NULL,
    AppId INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_llm_tools_static_id ON LlmTools(StaticId);
CREATE UNIQUE INDEX idx_llm_tools_name ON LlmTools(Name);
CREATE INDEX idx_llm_tools_app_id ON LlmTools(AppId);
CREATE INDEX idx_llm_tools_enabled_type ON LlmTools(IsEnabled, ToolType);
```
- `Id`: Unique integer identifier for the tool
- `StaticId`: Static identifier for the tool (e.g., "com.agent.filetools")
- `Name`: Display name of the tool (e.g., "WebSearch", "CodeGenerator")
- `Description`: Description of the tool's functionality
- `ToolConfig`: Information about the tool implementation
- `ToolType`: Integer value representing tool type (Normal=0)
- `Parameters`: JSON-serialized dictionary of tool parameters
- `IsEnabled`: Boolean flag (0 or 1) indicating if the tool is enabled
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- `State`: Integer value representing tool state (0=Stateless, 1=Stateful) indicating whether the tool maintains state between invocations
- `StateData`: Binary data for storing the tool's state (for stateful tools, supports lazy loading)
- `DevMsg`: Optional developer message for the LLM about tool usage
- `CreatedAt`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of creation time
- `AppId`: Integer identifier for the associated application (default: 0)
- Performance indexes on static ID lookup, name uniqueness, app ID, and composite enabled+type for efficient filtering

### LlmPrompts Table
```sql
CREATE TABLE LlmPrompts (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Category TEXT NOT NULL,
    Content TEXT NOT NULL,
    Parameters TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL,
    LastModified INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL
);

CREATE INDEX idx_llm_prompts_category_enabled ON LlmPrompts(Category, IsEnabled);
```
- `Id`: Unique integer identifier for the prompt
- `Name`: Display name of the prompt
- `Category`: Category for organizing prompts (e.g., "ChatBot", "CodeAssistant")
- `Content`: The actual prompt template text
- `Parameters`: JSON-serialized dictionary of prompt parameters
- `IsEnabled`: Boolean flag (0 or 1) indicating if the prompt is enabled
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- `CreatedAt`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of creation time
- Performance index on common filtering pattern (category + enabled status)

### Sessions Table
```sql
CREATE TABLE Sessions (
    Id INTEGER PRIMARY KEY,
    Title TEXT NOT NULL,
    Description TEXT NOT NULL,
    Logo BLOB,
    PersonNames TEXT NOT NULL,
    ToolNames TEXT NOT NULL DEFAULT '[]',
    ParentSessId INTEGER,
    CreatedAt INTEGER NOT NULL,
    LastModified INTEGER NOT NULL,
    TotalTokenCount INTEGER NOT NULL DEFAULT 0,
    OutputTokenCount INTEGER NOT NULL DEFAULT 0,
    InputTokenCount INTEGER NOT NULL DEFAULT 0,
    AdditionalCounts INTEGER NOT NULL DEFAULT 0,
    Properties TEXT NOT NULL DEFAULT '',
    SessionType INTEGER NOT NULL DEFAULT 0,
    AppId INTEGER NOT NULL DEFAULT 0,
    PreviousId INTEGER
);

CREATE INDEX idx_sessions_parent_sess_id ON Sessions(ParentSessId);
CREATE INDEX idx_sessions_session_type ON Sessions(SessionType);
CREATE INDEX idx_sessions_app_id ON Sessions(AppId);
CREATE INDEX idx_sessions_previous_id ON Sessions(PreviousId);
```
- `Id`: Unique integer identifier for the session
- `Title`: Display title of the session
- `Description`: Description of the session
- `Logo`: Binary data for the session logo (optional)
- `PersonNames`: JSON-serialized list of person names used in this session
- `ToolNames`: JSON-serialized list of tool names used in this session (default: '[]')
- `ParentSessId`: ID of the parent session (optional, for hierarchical session relationships)
- `CreatedAt`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of creation time
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- `TotalTokenCount`: Total number of tokens used in this session
- `OutputTokenCount`: Number of output tokens used in this session
- `InputTokenCount`: Number of input tokens used in this session
- `AdditionalCounts`: Additional token counts or metrics for the session
- `Properties`: JSON-serialized dictionary of additional session properties
- `SessionType`: Integer identifier for the type of session (default: 0)
- `AppId`: Integer identifier for the associated application (default: 0)
- `PreviousId`: ID of the previous session in a sequence
- Performance indexes on parent session ID, session type, app ID, and previous ID for efficient filtering

### Messages Table
```sql
CREATE TABLE Messages (
    Id INTEGER PRIMARY KEY,
    SessionId INTEGER NOT NULL,
    Content TEXT,
    Role TEXT NOT NULL,
    Type INTEGER NOT NULL,
    BinaryContent BLOB,
    BinaryVersion INTEGER,
    ParentMsgId INTEGER,
    ParentSessId INTEGER,
    CreatedAt INTEGER NOT NULL,
    LastModified INTEGER NOT NULL
);

CREATE INDEX idx_messages_session_id ON Messages(SessionId);
CREATE INDEX idx_messages_session_created ON Messages(SessionId, CreatedAt);
```
- `Id`: Unique integer identifier for the message
- `SessionId`: Foreign key to the Sessions table
- `Content`: The content of the message (nullable)
- `Role`: The role of the message stored as INTEGER values (Unknown=0, User=1, Assistant=2, System=3, Developer=4)
- `Type`: Integer representation of `DaoStudio.Interfaces.MessageType` (Normal=0, Information=1)
- `BinaryContent`: Binary data for storing serialized List<BinaryData> using MessagePack (stores files, images, or other binary content, supports lazy loading)
- `BinaryVersion`: Integer indicating the version of binary serialization (current version is 0)
- `ParentMsgId`: ID of the parent message that this message is responding to (for threaded conversations)
- `ParentSessId`: ID of the parent session for hierarchical session relationships
- `CreatedAt`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of creation time
- `LastModified`: Windows file time (100-nanosecond intervals since January 1, 1601 UTC) of last modification
- Performance indexes optimized for common message retrieval patterns by session and chronological order
- Supports selective binary data loading via `GetBySessionIdAsync(sessionId, includeBinaryData)` overload

### CachedModels Table
```sql
CREATE TABLE CachedModels (
    Id INTEGER PRIMARY KEY,
    ApiProviderId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    ModelId TEXT NOT NULL,
    ProviderType INTEGER NOT NULL,
    Catalog TEXT NOT NULL,
    Parameters TEXT NOT NULL DEFAULT '{}'
);

CREATE INDEX idx_cached_models_provider_id ON CachedModels(ApiProviderId);
CREATE INDEX idx_cached_models_compound ON CachedModels(ApiProviderId, ProviderType, Catalog);
```
- `Id`: Unique integer identifier for the cached model
- `ApiProviderId`: Foreign key to the APIProviders table
- `Name`: Display name of the model
- `ModelId`: Full unique identifier string for the model (e.g., "openai/gpt-4o")
- `ProviderType`: Integer identifier for provider type (same values as APIProviders.ProviderType; mapped to `DaoStudio.Interfaces.ProviderType`)
- `Catalog`: Catalog or category identifier for the model
- `Parameters`: JSON-serialized dictionary of model parameters (default: '{}')
- Performance indexes for provider-based queries and composite lookups

## Database Migration System

The library includes a migration system that uses SQLite's `user_version` pragma to track the current database version. Migrations are automatically applied when the application starts or when the `ApplyMigrations` method is called.

### Creating New Migrations

To create a new migration:

1. Create a class that inherits from `BaseMigration`
2. Set the `TargetVersion` property to a version number higher than the last migration
3. Implement the `ApplyAsync` method with the SQL commands to run
4. Register the migration in the `StorageFactory` class `InitializeAsync()` method or directly with the migration manager

Example migration:

```csharp
public class Migration_003_AddSearchIndex : BaseMigration
{
    public override int TargetVersion => 3;
    
    public override string Description => "Adds a search index to the LlmPrompts table";
    
    public override async Task<bool> ApplyAsync(SqliteConnection connection)
    {
        string sql = @"
            CREATE INDEX IF NOT EXISTS idx_llm_prompts_search 
            ON LlmPrompts(Category, Name);
        ";
        
        return await ExecuteSqlAsync(connection, sql);
    }
}
```

### Managing Migrations

```csharp
// Create and initialize a storage factory - use using statement for proper disposal
using var factory = new StorageFactory(GetDefaultDatabasePath());
await factory.InitializeAsync();

// Check the current database version
int version = await factory.GetDatabaseVersionAsync();

// Apply all pending migrations
bool migrationsApplied = await factory.ApplyMigrationsAsync();

// Create a factory with custom database path
using var customFactory = new StorageFactory("path/to/custom.db");
await customFactory.InitializeAsync();

// Apply migrations to the custom database
bool customMigrationsApplied = await customFactory.ApplyMigrationsAsync();

// Get access to the migration manager for advanced operations
var migrationManager = await factory.GetMigrationManagerAsync();
```
