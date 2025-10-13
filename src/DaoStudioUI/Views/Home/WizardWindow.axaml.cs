using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace DaoStudioUI;

public partial class WizardWindow : Window
{

    public WizardWindow()
    {
        InitializeComponent();
        
        
        // Enable window dragging from anywhere
        this.AddHandler(PointerPressedEvent, WindowDragging_PointerPressed, RoutingStrategies.Tunnel);
    }

    private void WindowDragging_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Get the visual element that was clicked
        var clickedElement = e.Source as Visual;
        
        // Don't start dragging if clicked on a control (Button, TextBox, etc.)
        if (clickedElement != null)
        {
            // Walk up the visual tree to see if we clicked on a control
            var element = clickedElement;
            while (element != null && element != this)
            {
                // If clicked on an interactive control, don't start dragging
                if (element is Button || 
                    element is TextBox || 
                    element is CheckBox || 
                    element is ComboBox ||
                    element is ListBox ||
                    element is Slider)
                {
                    return;
                }
                
                element = element.GetVisualParent();
            }
        }
        
        // If not clicking on a control, begin window drag
        BeginMoveDrag(e);
    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close();
    }
}