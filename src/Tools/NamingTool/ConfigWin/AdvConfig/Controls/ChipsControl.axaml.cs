using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Naming.AdvConfig.Controls
{
    public partial class ChipsControl : UserControl
    {
        public static readonly StyledProperty<ObservableCollection<string>> ItemsProperty =
            AvaloniaProperty.Register<ChipsControl, ObservableCollection<string>>(
                nameof(Items), 
                defaultValue: new ObservableCollection<string>());

        public static readonly StyledProperty<ICommand?> RemoveItemCommandProperty =
            AvaloniaProperty.Register<ChipsControl, ICommand?>(nameof(RemoveItemCommand));

        public ChipsControl()
        {
            InitializeComponent();
        }

        public ObservableCollection<string> Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public ICommand? RemoveItemCommand
        {
            get => GetValue(RemoveItemCommandProperty);
            set => SetValue(RemoveItemCommandProperty, value);
        }
    }
}
