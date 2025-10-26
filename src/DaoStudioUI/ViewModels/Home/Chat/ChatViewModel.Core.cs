using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaoStudio;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudioUI.Services;
using DesktopUI.Models;
using DesktopUI.Resources;
using DesktopUI.ViewModels.Home.Chat;
using DryIoc;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaoStudioUI.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    // Lock for subsession navigation to prevent concurrent access issues
    private readonly SemaphoreSlim _subsessionNavigationLock = new SemaphoreSlim(1, 1);

    // Event for session deletion notification (changed from static to instance)
    public event EventHandler<long>? SessionDeleted;

    // Event for session update notification
    public event EventHandler<long>? SessionUpdated;
    private Window? _currentWindow; // Added window reference

    public ISession Session { get; private set; } = null!;
    private IPerson _model = null!;

    [ObservableProperty]
    private ObservableCollection<IPerson> _availableModels = new();

    [ObservableProperty]
    private ObservableCollection<Models.ChatMessage> _messages = new();

    // Modified loading property that won't block UI when streaming
    private bool _isLoading = false;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            // When streaming is active, we don't want to fully block the UI
            // This ensures streaming responses are visible even while "loading"
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    [ObservableProperty]
    private bool _isRecording = false;

    [ObservableProperty]
    private bool _isPaneOpen = false;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _sessionTitle = string.Empty;

    //[ObservableProperty]
    //private string _modelInfo;

    [ObservableProperty]
    private byte[]? _modelImage;

    [ObservableProperty]
    private ToolsPanelViewModel _toolsPanelViewModel;

    // Navigation-related properties
    [ObservableProperty]
    private SessionNavigationStack? _navigationStack;

    partial void OnNavigationStackChanged(SessionNavigationStack? value)
    {
        // Notify the NavigateBackCommand that its CanExecute state may have changed
        NavigateBackCommand?.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbItems = new();

    [ObservableProperty]
    private NavigationDirection _navigationDirection = NavigationDirection.Forward;

    [ObservableProperty]
    private ChatSessionContent? _currentSessionContent;

    // Step debugging properties
    private DaoStudio.Filters.StepDebuggingFilter? _stepDebuggingFilter;

    [ObservableProperty]
    private bool _isStepDebuggingEnabled = false;


    // Injected dependencies
    private readonly IPeopleService _peopleService;
    private readonly ISessionService _sessionService;
    private readonly IMessageService _messageService;

    // Handler reference for OnMessageChanged event to allow proper unsubscribe
    private EventHandler<MessageChangedEventArgs>? _messageChangedHandler;

    // For DI without runtime parameters
    public ChatViewModel(IPeopleService peopleService, ISessionService sessionService, IMessageService messageService,
        ISession session, IPerson model, Window? currentWindow = null)
    {
        _peopleService = peopleService;
        _sessionService = sessionService;
        _messageService = messageService;

        Session = session;
        _model = model;
        _currentWindow = currentWindow; // Store the current window reference

        // Initialize with model name format, then load from database
        SessionTitle = model.Name;
        ModelImage = model.Image;

        // Load the session title from the database
        Task.Run(() => LoadSessionTitleAsync());

        // Create ToolsPanelViewModel with shared tool collection
        ToolsPanelViewModel = new ToolsPanelViewModel(this);

        // Initialize step debugging filter
        _stepDebuggingFilter = new DaoStudio.Filters.StepDebuggingFilter();
        _stepDebuggingFilter.WaitingStateChanged += OnFilterWaitingStateChanged;

        // Subscribe to consolidated session message event for real-time UI updates
        _messageChangedHandler = async (sender, e) =>
        {
            if (e.Change == MessageChangeType.New)
            {
                await HandleNewMessageAsync(e.Message);
            }
            else
            {
                await HandleMessageUpdateAsync(e.Message);
            }
        };
        Session.OnMessageChanged += _messageChangedHandler;

    // Subscribe to session event notifications from the session service
    _sessionService.SessionEvent += OnSessionEvent;

        // Subscribe to the consolidated person event
        _peopleService.PersonChanged += OnPersonChanged;

        // Subscribe to Session.PropertyChanged for SessionStatus changes
        Session.PropertyChanged += Session_PropertyChanged;

        // Load messages on initialization
        Task.Run(() => LoadMessagesAsync());

        // Load available tools
        Task.Run(() => LoadToolsAsync());

        // Load available models
        Task.Run(() => LoadPeopleAsync());

        // Setup property changed notifications
        this.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(Attachments))
            {
                OnPropertyChanged(nameof(HasAttachments));
            }
        };

        // Trigger Session_PropertyChanged to handle any session status changes that occurred during initialization
        Session_PropertyChanged(this, new PropertyChangeNotification(nameof(Session.SessionStatus)));
    }

    /// <summary>
    /// Handles property changes from the Session, specifically SessionStatus changes
    /// </summary>
    private void Session_PropertyChanged(object? sender, PropertyChangeNotification e)
    {
        if (e.PropertyName == nameof(Session.SessionStatus))
        {
            // Forward SessionStatus property changes to UI thread
            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(IsStreaming)));
        }
    }

    private async Task LoadPeopleAsync()
    {
        try
        {
            var models = await _peopleService.GetEnabledPersonsAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableModels.Clear();
                foreach (var model in models)
                {
                    AvailableModels.Add(model);
                }

                // Set the current model as selected
                SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == _model.Id) ?? _model;
            });

            Log.Information("Loaded {Count} models for dropdown", AvailableModels.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading models for dropdown");
            await DialogService.ShowExceptionAsync(
                ex,
                Strings.Settings_Error,
                _currentWindow);
        }
    }

    [ObservableProperty]
    private IPerson? _selectedModel;

    partial void OnSelectedModelChanged(IPerson? value)
    {
        if (value != null && value.Id != _model.Id)
        {
            _model = value;
            ModelImage = value.Image;


            Log.Information("Changed model to {ModelName} ({ModelId})", value.Name, value.ModelId);

            // Sync the model change to the Session
            Task.Run(async () =>
            {
                try
                {
                    // Update the model in the session
                    await Session.UpdatePersonAsync(value);
                    Log.Information("Synced model change to session {SessionId}", Session.Id);

                    // Notify listeners that a session was updated
                    SessionUpdated?.Invoke(this, Session.Id);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to sync model change to session {SessionId}", Session.Id);
                }
            });
        }
    }

    [RelayCommand]
    private void ChangeModel(IPerson model)
    {
        SelectedModel = model;
    }

    // Method to set the current window
    public void SetCurrentWindow(Window window)
    {
        _currentWindow = window;
    }

    [RelayCommand]
    private void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    // Handler method for the consolidated person event
    private void OnPersonChanged(object? sender, PersonOperationEventArgs args)
    {
        // Reload the models list when any person operation occurs
        Task.Run(() => LoadPeopleAsync());
    }

    // Method called when the window is closing
    public async void OnWindowClosing()
    {
        // Unsubscribe from the consolidated person event
        _peopleService.PersonChanged -= OnPersonChanged;

        // Clean up any resources or subscriptions if needed
        Log.Information("Chat window closing for session: {SessionId}", Session.Id);
        StopRecording();

        // Cancel any ongoing operations in the session
        Session.CurrentCancellationToken?.Cancel();

        // Unsubscribe from session events
        if (_messageChangedHandler != null)
        {
            Session.OnMessageChanged -= _messageChangedHandler;
            _messageChangedHandler = null;
        }

    // Unsubscribe from the session event notification
    _sessionService.SessionEvent -= OnSessionEvent;

        // Cleanup step debugging if active
        if (_stepDebuggingFilter != null)
        {
            if (IsStepDebuggingEnabled)
            {
                _stepDebuggingFilter.Disable();
                Session.RemoveFilter(_stepDebuggingFilter);
            }
            
            _stepDebuggingFilter.WaitingStateChanged -= OnFilterWaitingStateChanged;
            _stepDebuggingFilter = null;
        }

        // Check if this session is empty (no messages) and delete it if so
        try
        {
            var messages = await _messageService.GetMessagesBySessionIdAsync(Session.Id);
            if (!messages.Any())
            {
                await _sessionService.DeleteSessionAsync(Session.Id);
                Log.Information("Deleted empty session {SessionId} on window close", Session.Id);

                // Notify listeners that a session was deleted
                SessionDeleted?.Invoke(this, Session.Id);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Session.Description))
                {
                    await UpdateSessionDescriptionAsync(messages);
                }

                Session.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking/deleting empty session {SessionId}", Session.Id);

            // Show error dialog when session cleanup fails
            await DialogService.ShowExceptionAsync(
                ex,
                Strings.Settings_Error,
                _currentWindow);
        }
    }

    private async Task UpdateSessionDescriptionAsync(System.Collections.Generic.IEnumerable<IMessage> messages)
    {
        // Find the first user message and use it for session description
        var firstUserMessage = messages.FirstOrDefault(m => m.Role == MessageRole.User);
        if (firstUserMessage != null && !string.IsNullOrEmpty(firstUserMessage.Content))
        {
            // Truncate message to 60 chars if needed
            string description = firstUserMessage.Content.Length <= 60
                ? firstUserMessage.Content
                : firstUserMessage.Content.Substring(0, 57) + "...";

            // Update the session description using current session instance
            Session.Description = description;
            await _sessionService.SaveSessionAsync(Session);
            Log.Information("Updated session description for {SessionId}", Session.Id);
        }
    }

    private async Task HandleNewMessageAsync(IMessage message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Configure the message and add it with time marker support
            var chatMessage = ConfigureChatMessage(message);
            AddMessageWithTimeMarker(chatMessage);

            Log.Information("Added new message from event for session {SessionId}", Session.Id);
        });
    }

    private async Task HandleMessageUpdateAsync(IMessage message)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Find the existing message in the collection
            var existingMessage = Messages.FirstOrDefault(m => m.Id == message.Id);
            if (existingMessage != null)
            {
                // Update the content and timestamp
                existingMessage.Content = message.Content;
                existingMessage.CreatedAt = message.CreatedAt;
                existingMessage.LastModified = message.LastModified;

                // Trigger property changed notifications for derived properties
                existingMessage.OnPropertyChanged(nameof(existingMessage.HumanizedCreatedAt));
                existingMessage.OnPropertyChanged(nameof(existingMessage.LocalModifiedTime));

            }
            else
            {
                // If not found, treat as a new message and add it
                var chatMessage = ConfigureChatMessage(message);
                AddMessageWithTimeMarker(chatMessage);
                Log.Information("Added missing message {MessageId} on update for session {SessionId}", message.Id, Session.Id);
            }
        });
    }

    #region Navigation Methods

    /// <summary>
    /// Initializes the navigation stack with the current session as the root.
    /// </summary>
    public void InitializeNavigationStack()
    {
        if (NavigationStack == null)
        {
            NavigationStack = new SessionNavigationStack();
            var initialItem = new SessionNavigationItem(Session, _model)
            {
                ViewModel = this,
                Title = SessionTitle
            };
            NavigationStack.PushSession(initialItem);
            
            // Set the initial content for the TransitioningContentControl
            CurrentSessionContent = new ChatSessionContent(this);
            
            UpdateBreadcrumbs();
        }
    }

    /// <summary>
    /// Navigates to a session at the specified breadcrumb index.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToSessionAsync(int sessionIndex)
    {
        if (NavigationStack == null)
            return;

        var currentIndex = NavigationStack.CurrentIndex;
        if (sessionIndex == currentIndex)
            return;

        if (sessionIndex < 0 || sessionIndex >= NavigationStack.Sessions.Count)
            return;

        var direction = sessionIndex > currentIndex
            ? NavigationDirection.Forward
            : NavigationDirection.Backward;

        var targetItem = NavigationStack.NavigateToIndex(sessionIndex);
        if (targetItem == null)
            return;

        await PerformNavigationAsync(targetItem, direction);
    }

    /// <summary>
    /// Navigates back to the previous session in the stack.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private async Task NavigateBackAsync()
    {
        if (NavigationStack == null || !NavigationStack.CanNavigateBack)
            return;

        await NavigateToSessionAsync(NavigationStack.CurrentIndex - 1);
    }

    /// <summary>
    /// Determines if backward navigation is possible.
    /// </summary>
    private bool CanNavigateBack() => NavigationStack?.CanNavigateBack ?? false;

    /// <summary>
    /// Navigates to a child session (forward navigation).
    /// </summary>
    [RelayCommand]
    private async Task NavigateToChildSessionAsync(SessionNavigationItem childItem)
    {
        if (NavigationStack == null)
            return;

        await PerformNavigationAsync(childItem, NavigationDirection.Forward);
    }

    /// <summary>
    /// Performs the actual navigation with animation.
    /// </summary>
    private async Task PerformNavigationAsync(SessionNavigationItem targetItem, NavigationDirection direction)
    {
        try
        {
            NavigationDirection = direction;

            // Save current session state
            SaveCurrentSessionState();

            // Update current session in stack
            NavigationStack!.CurrentSession = targetItem;

            // Switch to new ViewModel
            CurrentSessionContent = new ChatSessionContent(targetItem.ViewModel!);

            // Update breadcrumbs
            UpdateBreadcrumbs();

            // Wait for animation
            await Task.Delay(350);

            // Restore target session state
            await RestoreSessionStateAsync(targetItem);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during navigation to session {SessionId}", targetItem.Session.Id);
        }
        finally
        {
            NavigateBackCommand?.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Updates the breadcrumb items based on the current navigation stack.
    /// </summary>
    private void UpdateBreadcrumbs()
    {
        if (NavigationStack == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            BreadcrumbItems.Clear();
            var path = NavigationStack.GetBreadcrumbPath();

            for (int i = 0; i < path.Count; i++)
            {
                var item = path[i];
                BreadcrumbItems.Add(new BreadcrumbItem
                {
                    Index = i,
                    Title = item.Title,
                    Icon = item.Icon,
                    IsCurrent = i == NavigationStack.CurrentIndex,
                    NavigateCommand = NavigateToSessionCommand
                });
            }
        });
    }

    /// <summary>
    /// Saves the current session's UI state before navigating away.
    /// </summary>
    private void SaveCurrentSessionState()
    {
        var currentItem = NavigationStack?.CurrentSession;
        if (currentItem == null)
            return;

        var state = currentItem.State ?? new SessionState();
        state.InputText = InputText;
        state.Attachments = Attachments.ToList();
        state.IsPaneOpen = IsPaneOpen;
        // ScrollPosition is managed via the view and preserved on the existing state instance
        currentItem.State = state;
    }

    /// <summary>
    /// Restores a session's UI state after navigating to it.
    /// </summary>
    private async Task RestoreSessionStateAsync(SessionNavigationItem item)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (item.State != null)
            {
                InputText = item.State.InputText;
                Attachments = new ObservableCollection<Attachment>(item.State.Attachments);
                IsPaneOpen = item.State.IsPaneOpen;
            }
        });

        // Allow UI to render before attempting scroll restoration
        await Task.Delay(100);
    }

    #endregion

    /// <summary>
    /// Loads the session title from the database and updates the UI
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task LoadSessionTitleAsync()
    {
        try
        {
            var title = Session.Title;
            if (!string.IsNullOrEmpty(title))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SessionTitle = title;
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load session title for session {SessionId}", Session.Id);
        }
    }

    /// <summary>
    /// Handles the SessionEvent by processing different session event types
    /// </summary>
    /// <param name="sender">The session service that raised the event</param>
    /// <param name="e">The session event arguments</param>
    private async void OnSessionEvent(object? sender, SessionEventArgs e)
    {
        // Only handle subsession creation events (Created with ParentSession)
        if (e.EventType != SessionEventType.Created || e.ParentSession == null)
        {
            return;
        }

        // Only process if the parent session matches our current session
        if (e.ParentSession.Id != Session.Id)
        {
            return;
        }

        var subsession = e.Session;

        try
        {
            if (NavigationStack == null)
            {
                Log.Warning("Navigation stack not initialized; skipping automatic subsession navigation for {SubsessionId}", subsession.Id);
                return;
            }

            await _subsessionNavigationLock.WaitAsync();
            try
            {

                // Check if the subsession's parent is the currently active session
                // This ensures we only navigate when the direct parent is active, not when a sibling or ancestor is active
                var currentSessionId = NavigationStack.CurrentSession?.Session.Id;
                if (currentSessionId != subsession.ParentSessionId)
                {
                    Log.Debug("Subsession {SubsessionId} created with parent {ParentId} but current session is {CurrentId}; no automatic navigation",
                        subsession.Id, subsession.ParentSessionId, currentSessionId);
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        var existingItem = FindNavigationItem(subsession.Id);
                        if (existingItem != null)
                        {
                            await PerformNavigationAsync(existingItem, NavigationDirection.Forward);
                            return;
                        }

                        var navigationItem = await CreateSubsessionNavigationItemAsync(subsession);
                        if (navigationItem == null)
                        {
                            return;
                        }

                        NavigationStack.PushSession(navigationItem);
                        await PerformNavigationAsync(navigationItem, NavigationDirection.Forward);

                        Log.Information("Navigated to subsession {SubsessionId} within current window", subsession.Id);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to navigate to subsession {SubsessionId}", subsession.Id);
                        await DialogService.ShowExceptionAsync(
                            ex,
                            "Failed to open subsession",
                            _currentWindow);
                    }
                });
            }
            finally
            {
                _subsessionNavigationLock.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in OnSessionEvent handler for session {SessionId}", Session.Id);
        }
    }

    private SessionNavigationItem? FindNavigationItem(long sessionId)
    {
        return NavigationStack?.Sessions.FirstOrDefault(item => item.Session.Id == sessionId);
    }

    private async Task<SessionNavigationItem?> CreateSubsessionNavigationItemAsync(ISession subsession)
    {
        var currentPerson = subsession.CurrentPerson;
        var person = await _peopleService.GetPersonAsync(currentPerson.Name);
        if (person == null)
        {
            Log.Warning("Could not find person {PersonName} for subsession {SubsessionId}", currentPerson.Name, subsession.Id);
            return null;
        }

        var getChatViewModel = App.GetContainer().Resolve<Func<ISession, IPerson, Window?, ChatViewModel>>();
        var subsessionViewModel = getChatViewModel(subsession, person, _currentWindow);

        subsessionViewModel.NavigationStack = NavigationStack;

        subsessionViewModel.SessionDeleted += (s, sessionId) =>
        {
            Log.Information("Subsession {SubsessionId} deleted", sessionId);
        };

        subsessionViewModel.SessionUpdated += (s, sessionId) =>
        {
            Log.Information("Subsession {SubsessionId} updated", sessionId);
        };

        return new SessionNavigationItem(subsession, person)
        {
            ViewModel = subsessionViewModel,
            Title = person.Name
        };
    }
}