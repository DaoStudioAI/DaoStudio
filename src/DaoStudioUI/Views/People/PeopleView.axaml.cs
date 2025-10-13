using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using System.Linq;
using DaoStudioUI.ViewModels;
using Avalonia.Interactivity;

namespace DaoStudioUI.Views
{
    public partial class ModelsView : UserControl
    {
        public ModelsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        
        private void DeleteButton_Tapped(object sender, Avalonia.Input.TappedEventArgs e)
        {
            // Mark as handled to prevent the event from bubbling up to the card
            e.Handled = true;
            // Get the model item from the DataContext of the button's parent
            if (sender is Button button &&
                button.DataContext is PersonItem modelItem &&
                DataContext is PeopleViewModel viewModel)
            {
                // Execute the delete command
                viewModel.DeleteModelCommand.Execute(modelItem);
            }
        }
    }

} 