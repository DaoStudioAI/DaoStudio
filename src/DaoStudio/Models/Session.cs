using DaoStudio.Common.Plugins;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using DaoStudio.Properties;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UsageDetails = DaoStudio.Interfaces.UsageDetails;

namespace DaoStudio;

/// <summary>
/// Represents a session in the Dao Studio.
/// </summary>
internal partial class Session : ISession
{
    private readonly IMessageService messageService;
    private readonly ISessionRepository sessionRepository;
    private readonly IToolService toolService;
    private DaoStudio.DBStorage.Models.Session dbsess;
    private IPerson person;
    private readonly ILogger<Session> logger; 
    private IPluginService pluginService;
    private IEngineService engineService;
    private IPeopleService peopleService;
    private IEngine? engine;

    private bool disposedValue;
    private bool _isInitialized = false;

    private SessionStatus _sessionStatus = SessionStatus.Idle;

    /// <summary>
    /// Event fired when the session is being disposed. Provides the session ID.
    /// </summary>
    public event Action<long>? SessionDisposed;

    /// <summary>
    /// Event fired when a property changes
    /// </summary>
    public event EventHandler<PropertyChangeNotification>? PropertyChanged;

    /// <summary>
    /// Event fired when a subsession is created.
    /// </summary>
    public event EventHandler<ISession>? SubsessionCreated;

    /// <summary>
    /// Fires the SubsessionCreated event when a subsession is created.
    /// </summary>
    /// <param name="subsession">The subsession that was created</param>
    public void FireSubsessionCreated(ISession subsession)
    {
        SubsessionCreated?.Invoke(this, subsession);
    }

    // Add Id property that exposes the underlying database session Id
    public long Id => dbsess.Id;

    /// <summary>
    /// Gets the parent session ID if this is a child session
    /// </summary>
    public long? ParentSessionId => dbsess.ParentSessId;

    /// <summary>
    /// Gets or sets the tool execution mode for chat interactions (IHostSession interface)
    /// </summary>
    public ToolExecutionMode ToolExecutionMode 
    { 
        get ;
        set;
    }


    public IPerson CurrentPerson => person;

    /// <summary>
    /// Gets or sets the current session status (Idle or Sending)
    /// </summary>
    public SessionStatus SessionStatus
    {
        get => _sessionStatus;
        set
        {
            if (_sessionStatus != value)
            {
                _sessionStatus = value;
                OnPropertyChanged(nameof(SessionStatus));
            }
        }
    }

    public DateTime CreatedAt { get => dbsess.CreatedAt; set => dbsess.CreatedAt = value; }
    public DateTime LastModified { get => dbsess.LastModified; set => dbsess.LastModified = value; }

    /// <summary>
    /// Gets or sets the title of the session
    /// </summary>
    public string Title { get => dbsess.Title; set => dbsess.Title = value; }

    /// <summary>
    /// Gets or sets the description of the session
    /// </summary>
    public string Description { get => dbsess.Description; set => dbsess.Description = value; }

    /// <summary>
    /// Raises the PropertyChanged event for the specified property
    /// </summary>
    /// <param name="propertyName">The name of the property that changed</param>
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangeNotification(propertyName));
    }

    public Task<List<IPerson>> GetPersonsAsync()
    {
        // Return the current person in a list
        return Task.FromResult(new List<IPerson> { person });
    }

    internal Session(IMessageService messageService,
        ISessionRepository sessionRepository,
        IToolService toolService,
        DaoStudio.DBStorage.Models.Session sess, IPerson person, 
        ILogger<Session> logger, IPluginService pluginService, IEngineService engineService,
        IPeopleService peopleService)
    {
        this.messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
        this.sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        this.toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
        this.dbsess = sess;
        this.person = person;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.pluginService = pluginService;
        this.engineService = engineService;
        this.peopleService = peopleService ?? throw new ArgumentNullException(nameof(peopleService));
        MsgMaxLoopCount = 100;
        
        // Subscribe to tool events to automatically refresh available tools
        this.toolService.ToolChanged += OnToolChanged;
        this.toolService.ToolListUpdated += OnToolListUpdated;
        
        // Subscribe to person changes to reload person when parameters change
        this.peopleService.PersonChanged += OnPersonChanged;
    }

    /// <summary>
    /// Initializes parent session mandatory filters if this is a child session
    /// </summary>
    /// <param name="parentSession">The parent session to propagate filters from</param>
    private void InitializeParentFilters(ISession parentSession)
    {
        if (parentSession == null)
        {
            throw new ArgumentNullException(nameof(parentSession));
        }

        // Add mandatory filters to propagate parent session's filters
        AddFilter(new Filters.ParentSessionMandatoryFilter(parentSession, FilterType.PreProcessing));
        AddFilter(new Filters.ParentSessionMandatoryFilter(parentSession, FilterType.PostProcessing));
    }

    /// <summary>
    /// Updates the LastModified timestamp of the session to the current time and saves it to the database
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UpdateSessionLastModifiedAsync()
    {
        dbsess.LastModified = DateTime.UtcNow;
        await sessionRepository.SaveSessionAsync(dbsess);
    }
    public async Task UpdatePersonAsync(IPerson person)
    {
        if (person == null)
        {
            throw new ArgumentNullException(nameof(person));
        }


        logger.LogInformation("Updating session {SessionId} person from {OldPersonName} to {NewPersonName}",
            Id, this.person.Name, person.Name);        // Close sessions for the old person's plugins
        await ClosePluginSessionsAsync();

        // Unsubscribe from previous engine events
        if (engine != null)
        {
            try
            {
                engine.UsageDetailsReceived -= Engine_UsageDetailsReceived;
            }
            catch { /* ignore */ }
        }

        // Dispose of the current engine if it exists
        if (engine is IDisposable disposableEngine)
        {
            disposableEngine.Dispose();
        }

        this.person = person;
        _isInitialized = false; // Reset initialization flag since person changed

        // Recreate the engine for the new person
        engine = await engineService.CreateEngineAsync(person);

        // Subscribe to new engine events
        if (engine != null)
        {
            engine.UsageDetailsReceived += Engine_UsageDetailsReceived;
        }

        dbsess.PersonNames = new List<string> { person.Name };
        dbsess.LastModified = DateTime.UtcNow;

        await sessionRepository.SaveSessionAsync(dbsess);

        // Update available plugins for the new person
        await UpdateAvailablePluginsAsync();
        _isInitialized = true; // Mark as initialized after plugins are updated
    }

    /// <summary>
    /// Handles the ToolListUpdated event from DaoStudio to refresh available tools
    /// </summary>
    /// <param name="sender">The event sender</param>
    /// <param name="e">The tool list update event arguments</param>
    private async void OnToolListUpdated(object? sender, ToolListUpdateEventArgs e)
    {
        try
        {
            // Only update if the session is initialized to avoid race conditions
            if (_isInitialized)
            {
                logger.LogDebug("Tool list updated for session {SessionId}, refreshing available plugins. Operation: {Operation}", 
                    Id, e.UpdateType);
                    
                // Update the available plugins based on the new tool list
                await UpdateAvailablePluginsAsync();
                
                logger.LogInformation("Session {SessionId} tool list refreshed due to {Operation} operation", 
                    Id, e.UpdateType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating session {SessionId} tools after tool list change", Id);
        }
    }

    /// <summary>
    /// Handles the ToolChanged event from DaoStudio to refresh available tools when needed
    /// </summary>
    /// <param name="sender">The event sender</param>
    /// <param name="e">The tool operation event arguments</param>
    private async void OnToolChanged(object? sender, ToolOperationEventArgs e)
    {
        try
        {
            // Only update if the session is initialized to avoid race conditions
            if (_isInitialized)
            {
                logger.LogDebug("Tool {Operation} for session {SessionId}, refreshing available plugins. Tool: {ToolName}", 
                    e.OperationType, Id, e.Tool?.Name ?? e.ToolId?.ToString());
                    
                // Update the available plugins based on the tool changes
                // This handles updates to tool properties like IsEnabled, Name changes, etc.
                await UpdateAvailablePluginsAsync();
                
                logger.LogInformation("Session {SessionId} tool list refreshed due to tool {Operation}", 
                    Id, e.OperationType);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating session {SessionId} tools after tool {Operation}", Id, e.OperationType);
        }
    }

    /// <summary>
    /// Handles the PersonChanged event from PeopleService to reload person when it changes
    /// </summary>
    /// <param name="sender">The event sender</param>
    /// <param name="e">The person operation event arguments</param>
    private async void OnPersonChanged(object? sender, PersonOperationEventArgs e)
    {
        try
        {
            // Only update if this is the current person and the session is initialized
            if (_isInitialized && e.OperationType == PersonOperationType.Updated && 
                e.Person != null && e.Person.Id == person.Id)
            {
                logger.LogInformation("Person {PersonName} updated for session {SessionId}, reloading person", 
                    person.Name, Id);
                    
                // Reload the person to get updated parameters
                var updatedPerson = await peopleService.GetPersonAsync(person.Name);
                if (updatedPerson != null)
                {
                    await UpdatePersonAsync(updatedPerson);
                    logger.LogInformation("Session {SessionId} person reloaded successfully", Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating session {SessionId} after person change", Id);
        }
    }



    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                try
                {
                    logger.LogInformation("Disposing session {SessionId}", Id);

                    // Unsubscribe from tool events to prevent memory leaks
                    toolService.ToolChanged -= OnToolChanged;
                    toolService.ToolListUpdated -= OnToolListUpdated;
                    
                    // Unsubscribe from person events
                    peopleService.PersonChanged -= OnPersonChanged;

                    // Unsubscribe from engine events regardless of disposability
                    if (engine != null)
                    {
                        try
                        {
                            engine.UsageDetailsReceived -= Engine_UsageDetailsReceived;
                        }
                        catch { /* ignore */ }
                    }

                    // Dispose of the engine if it exists and is disposable
                    if (engine is IDisposable disposableEngine)
                    {
                        disposableEngine.Dispose();
                    }

                    // Close plugin sessions. Using Wait() because Dispose is synchronous.
                    // This can be risky, but it's a common pattern in non-async dispose.
                    Task.Run(async () => await ClosePluginSessionsAsync()).Wait();

                    // Cancel all ongoing SendMessageAsync operations
                    try
                    {
                        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                        {
                            logger.LogInformation("Cancelling all ongoing operations for session {SessionId}", Id);
                            _cancellationTokenSource.Cancel();
                            _cancellationTokenSource.Dispose();
                        }

                        // Dispose the semaphore
                        _sendMessageSemaphore.Dispose();
                    }
                    catch (Exception cancelEx)
                    {
                        logger.LogError(cancelEx, "Error cancelling operations for session {SessionId}", Id);
                    }

                    logger.LogInformation("Finished disposing session {SessionId}", Id);

                    // Notify that the session has been disposed
                    SessionDisposed?.Invoke(Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during dispose for session {SessionId}", Id);
                }
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }



    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously initializes the session by setting up available plugins
    /// This method must be called after the constructor and before using the session
    /// </summary>
    /// <returns>A task representing the asynchronous initialization operation</returns>
    internal async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        logger.LogInformation("Initializing session {SessionId} for person {PersonName}", Id, person.Name);

        // Initialize available plugins based on person's tool names
        await UpdateAvailablePluginsAsync();
        
        if (engine == null)
        {
            engine = await engineService.CreateEngineAsync(person);
            if (engine != null)
            {
                engine.UsageDetailsReceived += Engine_UsageDetailsReceived;
            }
        }

        _isInitialized = true;
        logger.LogInformation("Session {SessionId} initialization completed", Id);
    }
    /// <summary>
    /// Ensures that the session is properly initialized before performing operations that require plugins
    /// If not initialized, it will automatically initialize the session
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }
    }

    /// <summary>
    /// Converts from Microsoft.Extensions.AI.ChatToolMode to ToolExecutionMode
    /// </summary>
    private static ToolExecutionMode ConvertToHostToolMode(Microsoft.Extensions.AI.ChatToolMode msaiMode)
    {
        if (msaiMode == Microsoft.Extensions.AI.ChatToolMode.Auto)
            return ToolExecutionMode.Auto;
        if (msaiMode == Microsoft.Extensions.AI.ChatToolMode.RequireAny)
            return ToolExecutionMode.RequireAny;
        if (msaiMode == Microsoft.Extensions.AI.ChatToolMode.None)
            return ToolExecutionMode.None;
        
        return ToolExecutionMode.Auto;
    }

    /// <summary>
    /// Converts from ToolExecutionMode to Microsoft.Extensions.AI.ChatToolMode
    /// </summary>
    private static Microsoft.Extensions.AI.ChatToolMode ConvertToMsaiToolMode(ToolExecutionMode hostMode)
    {
        return hostMode switch
        {
            ToolExecutionMode.Auto => Microsoft.Extensions.AI.ChatToolMode.Auto,
            ToolExecutionMode.RequireAny => Microsoft.Extensions.AI.ChatToolMode.RequireAny,
            ToolExecutionMode.None => Microsoft.Extensions.AI.ChatToolMode.None,
            _ => Microsoft.Extensions.AI.ChatToolMode.Auto
        };
    }

    /// <summary>
    /// Forwards usage details from the engine to session listeners.
    /// </summary>
    private void Engine_UsageDetailsReceived(object? sender, UsageDetails e)
    {
        try
        {
            // Treat incoming values as per-round usage; compute deltas relative to the last event.
            var curTotal = e.TotalTokens ?? 0;
            var curInput = e.InputTokens ?? 0;
            var curOutput = e.OutputTokens ?? 0;

     

            if (curTotal != 0 || curInput != 0 || curOutput != 0)
            {
                dbsess.TotalTokenCount += curTotal;
                dbsess.InputTokenCount += curInput;
                dbsess.OutputTokenCount += curOutput;


                // Persist asynchronously (fire-and-forget)
                _ = sessionRepository.SaveSessionAsync(dbsess);

            }

            UsageDetailsReceived?.Invoke(this, e);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error forwarding UsageDetailsReceived for session {SessionId}", Id);
        }
    }
}
