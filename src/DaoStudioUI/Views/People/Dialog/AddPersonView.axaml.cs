using DaoStudioUI.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace DaoStudioUI.Views.Dialogs
{
    public partial class AddModelView : Window
    {
        public AddModelView()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void TextBox_OnTextChanging(object? sender, TextChangingEventArgs e)
        {
            if (DataContext is AddPersonViewModel viewModel)
            {
                viewModel.HandleNameChanged();
            }
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is AddPersonViewModel viewModel)
                {
                    // Execute the cancel command
                    if (viewModel.CancelCommand.CanExecute(null))
                    {
                        viewModel.CancelCommand.Execute(null);
                    }
                }
                e.Handled = true;
            }
        }
    }
}