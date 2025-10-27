using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace DaoStudioUI.ViewModels;

/// <summary>
/// Partial class containing step debugging functionality for ChatViewModel
/// </summary>
public partial class ChatViewModel
{
    /// <summary>
    /// Toggles step debugging mode on or off
    /// </summary>
    [RelayCommand]
    private void ToggleStepDebugging()
    {
        if (IsStepDebuggingEnabled)
        {
            // Disable step debugging
            _stepDebuggingFilter?.Disable();  // Release any waiting tasks
            Session.RemoveFilter(_stepDebuggingFilter!);  // Remove filter from session pipeline
            IsStepDebuggingEnabled = false;
            Log.Information("Step debugging disabled for session {SessionId}", Session.Id);
        }
        else
        {
            // Enable step debugging
            _stepDebuggingFilter?.Enable();  // Activate the filter
            Session.AddFilter(_stepDebuggingFilter!);  // Add filter to session pipeline
            IsStepDebuggingEnabled = true;
            Log.Information("Step debugging enabled for session {SessionId}", Session.Id);
        }
    }
    
    /// <summary>
    /// Advances execution by one step when in step debugging mode.
    /// Only works when the filter is waiting for the next step.
    /// </summary>
    [RelayCommand]
    private void StepNext()
    {
        _stepDebuggingFilter?.StepNext();
        Log.Information("Step next triggered for session {SessionId}", Session.Id);
    }
    
    /// <summary>
    /// Event handler for when the filter's waiting state changes
    /// </summary>
    /// <param name="sender">The filter that raised the event</param>
    /// <param name="isWaiting">True if entering wait state, false if exiting</param>
    private void OnFilterWaitingStateChanged(object? sender, bool isWaiting)
    {
        // Update on UI thread
        Dispatcher.UIThread.Post(() =>
        {
            IsWaitingForStep = isWaiting;
            Log.Information("Step debugging waiting state changed to {IsWaiting} for session {SessionId}", 
                isWaiting, Session.Id);
        });
    }
}
