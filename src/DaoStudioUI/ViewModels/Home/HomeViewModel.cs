using DaoStudioUI.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaoStudio;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DryIoc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace DaoStudioUI.ViewModels;
public partial class HomeViewModel : ObservableObject
{
    // Static dictionary to track open session windows
    private static readonly Dictionary<long, Window> _openSessionWindows = new();
    private static readonly object _windowLock = new();
    [ObservableProperty]
    private ObservableCollection<IPerson> _models = new();

    [ObservableProperty]
    private ObservableCollection<SessionInfo> _sessions = new();
    
    [ObservableProperty]
    private bool _isModelsLoading = false;
    
    [ObservableProperty]
    private bool _hasModels = false;
    
    [ObservableProperty]
    private bool _hasSessions = false;

    private readonly IPeopleService _peopleService;
    private readonly ISessionService _sessionService;
    private readonly IMessageService _messageService;

    public HomeViewModel(IPeopleService peopleService, ISessionService sessionService, IMessageService messageService)
    {
        _peopleService = peopleService;
        _sessionService = sessionService;
        _messageService = messageService;
        
        // Load data on initialization
        Task.Run(() => LoadAllDataAsync());
    }
    
    // Handler for session deleted events
    private void OnSessionDeleted(object? sender, long sessionId)
    {
        // Refresh the sessions list when a session is deleted
        Task.Run(() => LoadSessionsAsync());
    }
    
    // Handler for session updated events
    private void OnSessionUpdated(object? sender, long sessionId)
    {
        // Refresh the sessions list when a session is updated
        Task.Run(() => LoadSessionsAsync());
    }

    [RelayCommand]
    private async Task LoadAllDataAsync()
    {
        await Task.WhenAll(
            LoadModelsAsync(),
            LoadSessionsAsync()
        );
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        if (IsModelsLoading) return;
        
        IsModelsLoading = true;
        
        try
        {
            // Load models
            var models = await _peopleService.GetAllPeopleAsync();
            
            // Dispatcher needed to update UI thread collection
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Models.Clear();
                
                foreach (var model in models)
                {
                    Models.Add(model);
                }
                
                HasModels = Models.Count > 0;
            });
        }
        catch (Exception ex)
        {
            await DialogService.ShowExceptionAsync(ex, "Failed to Load Models");
        }
        finally
        {
            IsModelsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {        
        try
        {
            // Load sessions - only include parent (top-level) sessions for the Home page
            var sessions = await _sessionService.GetAllSessionsAsync(includeParentSessions: true, includeChildSessions: false);
            
            // Sort sessions by LastModified in descending order
            var orderedSessions = sessions.OrderByDescending(s => s.LastModified).ToList();
            
            // Dispatcher needed to update UI thread collection
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateSessionsCollection(orderedSessions);
                HasSessions = Sessions.Count > 0;
            });
        }
        catch (Exception ex)
        {
            await DialogService.ShowExceptionAsync(ex, "Failed to Load Sessions");
        }
    }
    
    private void UpdateSessionsCollection(List<SessionInfo> newSessions)
    {
        // Create a lookup for existing sessions by ID for fast access
        var existingSessions = Sessions.ToDictionary(s => s.Id);
        var newSessionsLookup = newSessions.ToDictionary(s => s.Id);
        
        // Remove sessions that no longer exist
        for (int i = Sessions.Count - 1; i >= 0; i--)
        {
            var session = Sessions[i];
            if (!newSessionsLookup.ContainsKey(session.Id))
            {
                Sessions.RemoveAt(i);
            }
        }
        
        // Update existing sessions and add new ones
        for (int i = 0; i < newSessions.Count; i++)
        {
            var newSession = newSessions[i];
            
            if (existingSessions.TryGetValue(newSession.Id, out var existingSession))
            {
                // Check if the session has been modified
                if (existingSession.LastModified != newSession.LastModified ||
                    existingSession.Title != newSession.Title ||
                    existingSession.Description != newSession.Description)
                {
                    // Find the current index of the existing session
                    var currentIndex = Sessions.IndexOf(existingSession);
                    
                    // Remove the old version and insert the updated one at the correct position
                    Sessions.RemoveAt(currentIndex);
                    
                    // Insert at the correct sorted position
                    if (i < Sessions.Count)
                    {
                        Sessions.Insert(i, newSession);
                    }
                    else
                    {
                        Sessions.Add(newSession);
                    }
                }
                else
                {
                    // Session exists and hasn't changed, but check if it's in the right position
                    var currentIndex = Sessions.IndexOf(existingSession);
                    if (currentIndex != i && i < Sessions.Count)
                    {
                        // Move to correct position
                        Sessions.Move(currentIndex, i);
                    }
                }
            }
            else
            {
                // New session - insert at the correct position
                if (i < Sessions.Count)
                {
                    Sessions.Insert(i, newSession);
                }
                else
                {
                    Sessions.Add(newSession);
                }
            }
        }
    }
    
    [RelayCommand]
    private async Task OpenSession(SessionInfo sessionInfo)
    {
        try
        {
            Log.Information("Opening session: {SessionTitle}", sessionInfo.Title);

            // Check if window for this session is already open
            Window? existingWindow = null;
            lock (_windowLock)
            {
                _openSessionWindows.TryGetValue(sessionInfo.Id, out existingWindow);
            }
            
            if (existingWindow != null)
            {
                // Bring existing window to front
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    existingWindow.Show();
                    existingWindow.Activate();
                    existingWindow.BringIntoView();
                    
                    // If minimized, restore it
                    if (existingWindow.WindowState == WindowState.Minimized)
                    {
                        existingWindow.WindowState = WindowState.Normal;
                    }
                });
                
                Log.Information("Brought existing session window to front: {SessionTitle}", sessionInfo.Title);
                return;
            }

            var sess = await _sessionService.OpenSession(sessionInfo.Id);

            // Convert IPerson to Person by fetching from DaoStudio
            var currentPerson = sessionInfo.CurrentPerson;
            if (currentPerson == null)
            {
                Log.Warning("Session {SessionId} has no current person", sessionInfo.Id);
                return;
            }
            
            var model = await _peopleService.GetPersonAsync(currentPerson.Name);
            if (model == null)
            {
                Log.Warning("Could not find person {PersonName} for session", currentPerson.Name);
                return;
            }

            // Create the view model using factory
            var getChatViewModel = App.GetContainer().Resolve<Func<ISession, IPerson, Avalonia.Controls.Window?, ChatViewModel>>();
            var chatViewModel = getChatViewModel(sess, model, null);
            
            // Subscribe to the non-static SessionDeleted event
            chatViewModel.SessionDeleted += (sender, sessionId) => {
                // Refresh the sessions list when a session is deleted
                Task.Run(() => LoadSessionsAsync());
            };
            
            // Subscribe to the SessionUpdated event
            chatViewModel.SessionUpdated += OnSessionUpdated;

            // Create and show the window
            var chatWindow = new Views.ChatWindow();
            chatWindow.SetViewModel(chatViewModel);

            // Track the window and handle cleanup when closed
            lock (_windowLock)
            {
                _openSessionWindows[sessionInfo.Id] = chatWindow;
            }
            
            chatWindow.Closed += (sender, args) =>
            {
                lock (_windowLock)
                {
                    _openSessionWindows.Remove(sessionInfo.Id);
                    Log.Debug("Removed session window from tracking: {SessionId}", sessionInfo.Id);
                }
            };

            chatWindow.Show();
            Log.Information("Created new session window: {SessionTitle}", sessionInfo.Title);
        }
        catch (Exception ex)
        {
            await DialogService.ShowExceptionAsync(ex, $"Failed to Open Session: {sessionInfo.Title}");
        }
    }
    
    
    [RelayCommand]
    private async Task StartNewSess(IPerson model)
    {
        try
        {
            Log.Information("Creating session with model: {ModelName}", model.Name);


            // Save the session
            var createdSession = await _sessionService.CreateSession(model);

            // Create the view model using factory
            var getChatViewModel = App.GetContainer().Resolve<Func<ISession, IPerson, Avalonia.Controls.Window?, ChatViewModel>>();
            var chatViewModel = getChatViewModel(createdSession, model, null);
            
            // Subscribe to the non-static SessionDeleted event
            chatViewModel.SessionDeleted += (sender, sessionId) => {
                // Refresh the sessions list when a session is deleted
                Task.Run(() => LoadSessionsAsync());
            };
            
            // Subscribe to the SessionUpdated event
            chatViewModel.SessionUpdated += OnSessionUpdated;

            // Create and show the window
            var chatWindow = new Views.ChatWindow();
            chatWindow.SetViewModel(chatViewModel);

            chatWindow.Show();

            // Refresh sessions list
            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            await DialogService.ShowExceptionAsync(ex, $"Failed to Create Session with Model: {model.Name}");
        }
    }
    
    [RelayCommand]
    private async Task DeleteSession(SessionInfo sessionInfo)
    {
        if (sessionInfo == null) return;
        
        try
        {
            Log.Information("Deleting session: {SessionTitle}", sessionInfo.Title);
            
            var result = await _sessionService.DeleteSessionAsync(sessionInfo.Id);
            
            if (result)
            {                
                // Remove the session from the observable collection directly
                // This avoids reloading the entire session list
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Sessions.Remove(sessionInfo);
                    HasSessions = Sessions.Count > 0;
                });
            }
        }
        catch (Exception ex)
        {
            await DialogService.ShowExceptionAsync(ex, $"Failed to Delete Session: {sessionInfo.Title}");
        }
    }
}