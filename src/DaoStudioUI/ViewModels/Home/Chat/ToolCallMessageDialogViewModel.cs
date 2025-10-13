using System;
using System.Threading.Tasks;
using DaoStudioUI.Models;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaoStudioUI.Resources;
using DesktopUI.Resources;

namespace DaoStudioUI.ViewModels;

public partial class ToolCallMessageDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = Strings.ToolCall_DialogTitle;

    [ObservableProperty]
    private string? _messageContent;

    private readonly ChatMessage _message;
    private Window? _dialogWindow;

    public ToolCallMessageDialogViewModel(ChatMessage message)
    {
        _message = message;
        _messageContent = message.Content;
    }

    public void SetDialogWindow(Window window)
    {
        _dialogWindow = window;
    }

    [RelayCommand]
    private void Save()
    {
        _message.Content = MessageContent;
        _message.SaveEditCommand.Execute(null);
        _dialogWindow?.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogWindow?.Close();
    }
} 