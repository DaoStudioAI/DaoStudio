using Avalonia.Controls;
using Avalonia.Interactivity;
using DaoStudioUI.ViewModels;
using DaoStudioUI.Models;

namespace DaoStudioUI.Views;

public partial class ToolCallMessageDialog : Window
{
    public ToolCallMessageDialog()
    {
        InitializeComponent();
    }

    public ToolCallMessageDialog(ChatMessage message) : this()
    {
        var viewModel = new ToolCallMessageDialogViewModel(message);
        DataContext = viewModel;
        viewModel.SetDialogWindow(this);
    }
} 