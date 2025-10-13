using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DaoStudioUI.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Input;
using DaoStudio;
using DaoStudioUI.Models;
using DaoStudioUI.Converters;
using DaoStudioUI.Services;
using Serilog;
using FluentAvalonia.UI.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using DaoStudio.Interfaces;

namespace DaoStudioUI.Views
{
    public partial class ChatWindow : Window
    {
    private ScrollViewer? _messageScroller;
    private bool _isRestoringScroll;
        private TextBox? _messageInputTextBox;
        private MenuFlyout? _modelSelectionFlyout;
        private ChatViewModel? _currentChatViewModel;
        private INotifyCollectionChanged? _currentAvailableModels;
    private static readonly IBrush ModelIdBrush = Brush.Parse("#777777");
        
        public ChatWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            
            
            // Find scroll viewer for auto-scrolling
            _messageScroller = this.FindControl<ScrollViewer>("MessageScroller");
            
            // Find message input TextBox for focus handling
            _messageInputTextBox = this.FindControl<TextBox>("MessageInputTextBox");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _messageScroller = this.FindControl<ScrollViewer>("MessageScroller");
            _messageInputTextBox = this.FindControl<TextBox>("MessageInputTextBox");
            InitializeModelSelectionFlyout();
        }

        private void OnMessageScrollerLoaded(object? sender, RoutedEventArgs e)
        {
            if (sender is not ScrollViewer scroller)
            {
                return;
            }

            if (!ReferenceEquals(_messageScroller, scroller))
            {
                if (_messageScroller != null)
                {
                    _messageScroller.PropertyChanged -= OnMessageScrollerPropertyChanged;
                }

                _messageScroller = scroller;
            }

            _messageScroller.PropertyChanged += OnMessageScrollerPropertyChanged;
            RestoreScrollPosition();
        }

        private void OnMessageScrollerUnloaded(object? sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer scroller)
            {
                scroller.PropertyChanged -= OnMessageScrollerPropertyChanged;

                if (ReferenceEquals(_messageScroller, scroller))
                {
                    _messageScroller = null;
                }
            }
        }

        private void OnMessageScrollerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_isRestoringScroll || sender is not ScrollViewer || e.Property != ScrollViewer.OffsetProperty)
            {
                return;
            }

            if (DataContext is not ChatViewModel viewModel)
            {
                return;
            }

            if (e is AvaloniaPropertyChangedEventArgs<Vector> vectorArgs)
            {
                var offset = vectorArgs.NewValue.GetValueOrDefault();
                var currentItem = viewModel.NavigationStack?.CurrentSession;
                if (currentItem?.State != null)
                {
                    currentItem.State.ScrollPosition = offset.Y;
                }
            }
        }

        private void RestoreScrollPosition()
        {
            if (_messageScroller == null)
            {
                return;
            }

            if (DataContext is not ChatViewModel viewModel)
            {
                return;
            }

            var state = viewModel.NavigationStack?.CurrentSession?.State;
            if (state == null)
            {
                return;
            }

            _isRestoringScroll = true;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (_messageScroller != null)
                    {
                        var currentOffset = _messageScroller.Offset;
                        var targetOffset = new Vector(currentOffset.X, state.ScrollPosition);
                        _messageScroller.Offset = targetOffset;
                    }
                }
                finally
                {
                    _isRestoringScroll = false;
                }
            }, DispatcherPriority.Background);
        }
        
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            
            // Set focus to the message input TextBox when window opens
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _messageInputTextBox?.Focus();
            });
        }
        
        private void ScrollToBottom()
        {
            // Ensure UI thread and delay slightly to allow layout
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(100);
                if (_messageScroller != null && !_isRestoringScroll)
                {
                    _messageScroller.ScrollToEnd();
                }
            });
        }
        
        // Method to set the ViewModel and subscribe to back button events
        public void SetViewModel(ChatViewModel viewModel)
        {
            // Pass the current window reference to the ViewModel
            viewModel.SetCurrentWindow(this);
            
            // Initialize navigation stack with the current session
            viewModel.InitializeNavigationStack();
            
            DataContext = viewModel;
            
            // Set the window icon based on the model image
            UpdateWindowIcon(viewModel.ModelImage);
            
            // Subscribe to ModelImage property changes to update the window icon
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ChatViewModel.ModelImage))
                {
                    UpdateWindowIcon(viewModel.ModelImage);
                }
            };
            
            // Handle window closing to prevent model/session leaks
            Closing += (sender, args) => viewModel.OnWindowClosing();
            
            // Subscribe to property changes to auto-scroll
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(ChatViewModel.Messages))
                {
                    ScrollToBottom();
                }
                // Also scroll when IsStreaming changes to ensure visibility
                else if (args.PropertyName == nameof(ChatViewModel.IsStreaming))
                {
                    ScrollToBottom();
                }
            };
            
            // Subscribe to message collection changes
            viewModel.Messages.CollectionChanged += (sender, args) =>
            {
                ScrollToBottom();
            };
            
            // Special handler for message content updates during streaming
            if (viewModel.Messages.Count > 0)
            {
                foreach (var message in viewModel.Messages)
                {
                    message.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(Models.ChatMessage.Content) && 
                            viewModel.IsStreaming)
                        {
                            ScrollToBottom();
                        }
                    };
                }
            }
            
            // Setup custom key handling for line breaks with proper cursor positioning
            if (_messageInputTextBox != null)
            {
                _messageInputTextBox.KeyDown += OnMessageInputKeyDown;
            }

            AttachModelMenuHandlers(viewModel);
        }
        
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            
            // Subscribe to property changes when data context changes
            if (DataContext is ChatViewModel viewModel)
            {
                AttachModelMenuHandlers(viewModel);

                viewModel.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(ChatViewModel.Messages))
                    {
                        ScrollToBottom();
                    }
                    // Also scroll when IsStreaming changes to ensure visibility
                    else if (args.PropertyName == nameof(ChatViewModel.IsStreaming))
                    {
                        ScrollToBottom();
                    }
                };
                
                // Subscribe to message collection changes to detect streaming updates
                viewModel.Messages.CollectionChanged += (sender, args) =>
                {
                    ScrollToBottom();
                };
            }
            else
            {
                DetachModelMenuHandlers();
            }
        }
          // Event handler for message content click to start editing
        private void OnMessageContentPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is Models.ChatMessage message)
            {
                message.StartEditing();
            }
        }
        
        // Helper method to update the window icon based on the model image
        private void UpdateWindowIcon(byte[]? modelImage)
        {
            if (modelImage != null && modelImage.Length > 0)
            {
                try
                {
                    // Convert byte array to Bitmap using the same converter used in XAML
                    var converter = new ByteArrayToImageConverter();
                    var bitmap = converter.Convert(modelImage, typeof(Bitmap), null, CultureInfo.CurrentCulture) as Bitmap;
                    
                    if (bitmap != null)
                    {
                        // Convert the bitmap to a WindowIcon before setting it as the window icon
                        using (var memoryStream = new MemoryStream())
                        {
                            bitmap.Save(memoryStream);
                            memoryStream.Position = 0;
                            this.Icon = new WindowIcon(memoryStream);
                        }
                    }
                    else
                    {
                        // Fallback to default icon if conversion fails
                        this.Icon = CreateIconFromSymbol(FluentAvalonia.UI.Controls.Symbol.Contact);
                    }
                }
                catch (Exception ex)
                {
                    // Log the error and fallback to default icon
                    Log.Error(ex, "Error setting window icon");
                    this.Icon = CreateIconFromSymbol(Symbol.Contact);
                }
            }
            else
            {
                // Use default icon if no model image is available
                this.Icon = CreateIconFromSymbol(Symbol.Contact);
            }
        }

        // Helper method to create a WindowIcon from a Symbol
        private WindowIcon CreateIconFromSymbol(FluentAvalonia.UI.Controls.Symbol symbol, double size = 128) // Default size 16x16 for window icons
        {
            try
            {
                var symbolIcon = new FluentAvalonia.UI.Controls.SymbolIcon { Symbol = symbol, FontSize = size };

                // Measure with infinite space to get the desired size based on content
                symbolIcon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var iconDesiredSize = symbolIcon.DesiredSize;

                // Use the desired size, or the specified 'size' if desired size is zero (e.g., if not properly measured yet or for consistency)
                var finalSize = new Size(
                    iconDesiredSize.Width > 0 && iconDesiredSize.Height > 0 ? iconDesiredSize.Width : size,
                    iconDesiredSize.Width > 0 && iconDesiredSize.Height > 0 ? iconDesiredSize.Height : size
                );
                // Ensure the control itself is also sized to what we intend to render
                symbolIcon.Width = finalSize.Width;
                symbolIcon.Height = finalSize.Height;

                symbolIcon.Measure(finalSize);
                symbolIcon.Arrange(new Rect(finalSize));

                var pixelWidth = Math.Max(1, (int)Math.Ceiling(finalSize.Width));
                var pixelHeight = Math.Max(1, (int)Math.Ceiling(finalSize.Height));

                var renderTargetBitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96, 96)); // DPI 96,96

                using (var drawingContext = renderTargetBitmap.CreateDrawingContext())
                {
                    drawingContext.FillRectangle(Brushes.Transparent, new Rect(0, 0, pixelWidth, pixelHeight)); // Ensure transparent background for the bitmap
                    symbolIcon.Render(drawingContext);
                }

                using (var memoryStream = new MemoryStream())
                {
                    renderTargetBitmap.Save(memoryStream); // Saves as PNG by default
                    memoryStream.Position = 0;
                    return new WindowIcon(memoryStream);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating icon from symbol");
                // Fallback to the original asset if symbol rendering fails
                return new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares:///Assets/logo.ico"))));
            }
        }
        
        /// <summary>
        /// Handles key input for the message TextBox to provide proper cursor positioning for line breaks
        /// </summary>
        private void OnMessageInputKeyDown(object? sender, KeyEventArgs e)
        {
            // Handle Ctrl+Enter, Shift+Enter, Alt+Enter for line breaks
            if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || 
                                      e.KeyModifiers.HasFlag(KeyModifiers.Shift) || 
                                      e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
            {
                e.Handled = true; // Prevent default behavior
                
                if (_messageInputTextBox != null && DataContext is ChatViewModel viewModel)
                {
                    // Get the current caret position
                    int caretPosition = _messageInputTextBox.CaretIndex;
                    
                    // Insert newline at the current position
                    string currentText = viewModel.InputText ?? string.Empty;
                    string newText = currentText.Insert(caretPosition, Environment.NewLine);
                    viewModel.InputText = newText;
                    
                    // Update the cursor position to after the inserted newline
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _messageInputTextBox.CaretIndex = caretPosition + Environment.NewLine.Length;
                    });
                }
            }
        }

        private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Only handle left mouse button for dragging
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private void InitializeModelSelectionFlyout()
        {
            if (!Resources.TryGetResource("ModelSelectionFlyout", ActualThemeVariant, out var resource))
            {
                Resources.TryGetResource("ModelSelectionFlyout", ThemeVariant.Default, out resource);
            }

            if (resource is MenuFlyout flyout)
            {
                _modelSelectionFlyout = flyout;
            }
        }

        private void AttachModelMenuHandlers(ChatViewModel viewModel)
        {
            if (ReferenceEquals(_currentChatViewModel, viewModel))
            {
                UpdateModelFlyoutItems();
                return;
            }

            DetachModelMenuHandlers();

            _currentChatViewModel = viewModel;
            _currentAvailableModels = viewModel.AvailableModels;

            if (_currentAvailableModels != null)
            {
                _currentAvailableModels.CollectionChanged += OnAvailableModelsChanged;
            }

            viewModel.PropertyChanged += OnChatViewModelPropertyChangedForMenu;

            UpdateModelFlyoutItems();
        }

        private void DetachModelMenuHandlers()
        {
            if (_currentChatViewModel != null)
            {
                _currentChatViewModel.PropertyChanged -= OnChatViewModelPropertyChangedForMenu;
            }

            if (_currentAvailableModels != null)
            {
                _currentAvailableModels.CollectionChanged -= OnAvailableModelsChanged;
            }

            _currentChatViewModel = null;
            _currentAvailableModels = null;
        }

        private void OnChatViewModelPropertyChangedForMenu(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not ChatViewModel viewModel)
            {
                return;
            }

            if (e.PropertyName == nameof(ChatViewModel.AvailableModels))
            {
                if (_currentAvailableModels != null)
                {
                    _currentAvailableModels.CollectionChanged -= OnAvailableModelsChanged;
                }

                _currentAvailableModels = viewModel.AvailableModels;

                if (_currentAvailableModels != null)
                {
                    _currentAvailableModels.CollectionChanged += OnAvailableModelsChanged;
                }

                UpdateModelFlyoutItems();
            }
            else if (e.PropertyName == nameof(ChatViewModel.SelectedModel))
            {
                UpdateModelFlyoutItems();
            }
        }

        private void OnAvailableModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                UpdateModelFlyoutItems();
            }
            else
            {
                Dispatcher.UIThread.Post(UpdateModelFlyoutItems);
            }
        }

        private void UpdateModelFlyoutItems()
        {
            if (_modelSelectionFlyout == null || _currentChatViewModel == null)
            {
                return;
            }

            void Update()
            {
                _modelSelectionFlyout.Items.Clear();

                var selectedModel = _currentChatViewModel.SelectedModel;

                foreach (var model in _currentChatViewModel.AvailableModels)
                {
                    _modelSelectionFlyout.Items.Add(CreateModelMenuItem(model, selectedModel));
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Update();
            }
            else
            {
                Dispatcher.UIThread.Post(Update);
            }
        }

        private MenuItem CreateModelMenuItem(IPerson model, IPerson? selectedModel)
        {
            var headerContent = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2
            };

            headerContent.Children.Add(new TextBlock
            {
                Text = model.Name,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12
            });

            headerContent.Children.Add(new TextBlock
            {
                Text = model.ModelId,
                FontStyle = FontStyle.Italic,
                Foreground = ModelIdBrush,
                FontSize = 10
            });

            return new MenuItem
            {
                Header = headerContent,
                Icon = new SymbolIcon
                {
                    Symbol = Symbol.Contact,
                    FontSize = 14
                },
                Command = _currentChatViewModel?.ChangeModelCommand,
                CommandParameter = model,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = selectedModel != null && selectedModel.Id == model.Id
            };
        }
    }
}