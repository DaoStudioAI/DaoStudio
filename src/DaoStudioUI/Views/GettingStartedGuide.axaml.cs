using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DaoStudioUI.Views
{
    public partial class GettingStartedGuide : UserControl
    {
        public event System.EventHandler<string>? NavigationRequested;

        public GettingStartedGuide()
        {
            InitializeComponent();
        }

        private void ApiProviderLink_Click(object? sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Settings");
        }

        private void PersonLink_Click(object? sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "People");
        }

        private void ToolsLink_Click(object? sender, RoutedEventArgs e)
        {
            NavigationRequested?.Invoke(this, "Tools");
        }
    }
}
