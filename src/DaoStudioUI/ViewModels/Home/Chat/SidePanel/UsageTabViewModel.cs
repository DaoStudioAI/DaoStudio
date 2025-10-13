using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DaoStudio;
using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using Microsoft.Extensions.AI;
using Serilog;

namespace DaoStudioUI.ViewModels
{
    public partial class UsageTabViewModel : ObservableObject
    {
        private readonly ChatViewModel _chatViewModel;
        private ISession? _currentSession;

        // Session accumulated tokens
        [ObservableProperty]
        private long _totalTokenCount;

        [ObservableProperty]
        private long _inputTokenCount;

        [ObservableProperty]
        private long _outputTokenCount;

        // Current round tokens
        [ObservableProperty]
        private long _currentRoundTotalTokens;

        [ObservableProperty]
        private long _currentRoundInputTokens;

        [ObservableProperty]
        private long _currentRoundOutputTokens;

        [ObservableProperty]
        private Dictionary<string, string> _additionalCounts = new();

        public UsageTabViewModel(ChatViewModel chatViewModel)
        {
            _chatViewModel = chatViewModel;
            
            // Subscribe to the ChatViewModel's PropertyChanged event to detect when Session changes
            _chatViewModel.PropertyChanged += ChatViewModel_PropertyChanged;
            
            // Initialize with the current session if available
            InitializeWithCurrentSession();
        }

        private void ChatViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatViewModel.Session))
            {
                // Session has changed, update our subscription
                UnsubscribeFromCurrentSession();

                // Reset current-round tokens on session change so UI doesn't show stale values
                Dispatcher.UIThread.Post(() =>
                {
                    CurrentRoundTotalTokens = 0;
                    CurrentRoundInputTokens = 0;
                    CurrentRoundOutputTokens = 0;
                });

                InitializeWithCurrentSession();
            }
        }

        private void InitializeWithCurrentSession()
        {
            if (_chatViewModel.Session != null && !ReferenceEquals(_currentSession, _chatViewModel.Session))
            {
                _currentSession = _chatViewModel.Session;
                _currentSession.UsageDetailsReceived += OnUsageDetailsReceived;
                
                // Initialize with current session data
                UpdateFromSession();
            }
        }

        private void UpdateFromSession()
        {
            if (_currentSession is null) return;
            
            // Update on UI thread to avoid cross-thread access issues
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // Update accumulated session tokens
                    TotalTokenCount = _currentSession.TotalTokenCount;
                    InputTokenCount = _currentSession.InputTokenCount;
                    OutputTokenCount = _currentSession.OutputTokenCount;
                    // Do NOT reset current round tokens here. This method is called during view load
                    // and session re-initialization; resetting here would overwrite the latest values
                    // that were already provided via OnUsageDetailsReceived.
                    
                    // AdditionalTokenProperties now directly returns the deserialized Dictionary<string, long>
                    AdditionalCounts = _currentSession.AdditionalTokenProperties ?? new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error updating usage data from session");
                }
            });
        }

        private void OnUsageDetailsReceived(object? sender, DaoStudio.Interfaces.UsageDetails details)
        {
            // Update the view model properties with the latest usage details
            // Use the UI thread for updates to avoid cross-thread access issues
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // Update current round tokens from the incoming details
                    CurrentRoundTotalTokens = details.TotalTokens?? 0;
                    CurrentRoundInputTokens = details.InputTokens ?? 0;
                    CurrentRoundOutputTokens = details.OutputTokens ?? 0;
                    
                    // Get accumulated session tokens from the session
                    if (_currentSession is not null)
                    {
                        TotalTokenCount = _currentSession.TotalTokenCount;
                        InputTokenCount = _currentSession.InputTokenCount;
                        OutputTokenCount = _currentSession.OutputTokenCount;
                    }
                    
                    if (details.AdditionalProperties != null)
                    {
                        AdditionalCounts = details.AdditionalProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error updating usage data from event");
                }
            });
        }

        private void UnsubscribeFromCurrentSession()
        {
            if (_currentSession is not null)
            {
                _currentSession.UsageDetailsReceived -= OnUsageDetailsReceived;
                _currentSession = null;
            }
        }
        public void OnLoad()
        {
            // Reattach to ChatViewModel.PropertyChanged to track session switches while the view is active.
            _chatViewModel.PropertyChanged -= ChatViewModel_PropertyChanged;
            _chatViewModel.PropertyChanged += ChatViewModel_PropertyChanged;

            // Ensure we're subscribed to the current session without resetting current-round values
            InitializeWithCurrentSession();
        }

        public void OnUnload()
        {
            _chatViewModel.PropertyChanged -= ChatViewModel_PropertyChanged;
            UnsubscribeFromCurrentSession();
        }
    }
}
