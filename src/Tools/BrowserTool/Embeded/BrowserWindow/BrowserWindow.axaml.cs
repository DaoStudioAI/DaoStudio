using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using Xilium.CefGlue;
using Xilium.CefGlue.Avalonia;
using Xilium.CefGlue.Common.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using AngleSharp.Html;
using AngleSharp.Dom;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using BrowserTool.Properties;
using Serilog;

namespace BrowserTool
{

    public class MyAvaloniaCefBrowser: AvaloniaCefBrowser
    {
        public CefBrowser CefBrowser => UnderlyingBrowser;
    }

    public partial class BrowserWindow : Window
    {
        private readonly List<BrowserTab> _tabs = new List<BrowserTab>();
        private BrowserTab? _activeTab = null;
        private TextBox _addressBar = null!;
        private StackPanel _tabContainer = null!;
        private Decorator _browserWrapper = null!;
        private Grid _welcomeScreen = null!;
        private readonly bool _enableSessionAware;
        
        // Legacy compatibility property
        public MyAvaloniaCefBrowser? browser => _activeTab?.Browser;

        public BrowserWindow() : this(true)
        {
        }

        public BrowserWindow(bool enableSessionAware)
        {
            _enableSessionAware = enableSessionAware;
            AvaloniaXamlLoader.Load(this);
            InitializeControls();
            ShowWelcomeScreen();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeControls()
        {
            _addressBar = this.FindControl<TextBox>("AddressBar") ??
                throw new InvalidOperationException(string.Format(Properties.Resources.Error_ControlNotFound, "AddressBar"));
            
            _tabContainer = this.FindControl<StackPanel>("TabContainer") ??
                throw new InvalidOperationException(string.Format(Properties.Resources.Error_ControlNotFound, "TabContainer"));
            
            _browserWrapper = this.FindControl<Decorator>("browserWrapper") ??
                throw new InvalidOperationException(string.Format(Properties.Resources.Error_ControlNotFound, "browserWrapper"));
            
            _welcomeScreen = this.FindControl<Grid>("WelcomeScreen") ??
                throw new InvalidOperationException(string.Format(Properties.Resources.Error_ControlNotFound, "WelcomeScreen"));
        }

        private void ShowWelcomeScreen()
        {
            _welcomeScreen.IsVisible = true;
            _browserWrapper.Child = null;
            _addressBar.Text = "";
            Title = Properties.Resources.BrowserToolDisplayName;
        }

        public BrowserTab CreateNewTab(string url = "", long? sessionId = null)
        {
            var tab = new BrowserTab(url)
            {
                SessionId = sessionId
            };
            
            // Setup browser events
            tab.Browser.TitleChanged += (sender, title) => OnTabTitleChanged(tab, title);
            tab.Browser.AddressChanged += (sender, address) => OnTabAddressChanged(tab, address);
            tab.Browser.LoadStart += (sender, e) => OnTabLoadStart(tab, e);
            tab.Browser.LoadEnd += (sender, e) => OnTabLoadEnd(tab, e);
            
            // Create tab button
            tab.TabButton = CreateTabButton(tab);
            
            _tabs.Add(tab);
            
            // Add tab button to container (before the new tab button)
            var newTabButton = _tabContainer.Children.LastOrDefault();
            if (newTabButton != null)
            {
                _tabContainer.Children.Insert(_tabContainer.Children.Count - 1, tab.TabButton);
            }
            else
            {
                _tabContainer.Children.Add(tab.TabButton);
            }
            
            return tab;
        }

        private string FormatTabTitle(BrowserTab tab)
        {
            if (_enableSessionAware && tab.SessionId.HasValue)
            {
                return $"{tab.SessionId.Value:X}|{tab.Title}";
            }
            return tab.Title;
        }

        private Button CreateTabButton(BrowserTab tab)
        {
            var tabButton = new Button
            {
                Classes = { "tab" },
                Height = 36,
                MinWidth = 160,
                MaxWidth = 240,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };

            var titleBlock = new TextBlock
            {
                Text = FormatTabTitle(tab),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
                FontSize = 13,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(titleBlock, 0);

            var closeButton = new Button
            {
                Classes = { "tab-close" },
                Content = new SymbolIcon { Symbol = Symbol.Cancel, FontSize = 12 },
                Width = 20,
                Height = 20,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(closeButton, 1);

            grid.Children.Add(titleBlock);
            grid.Children.Add(closeButton);
            tabButton.Content = grid;

            // Tab button click event
            tabButton.Click += (sender, e) => ActivateTab(tab);
            
            // Close button click event
            closeButton.Click += (sender, e) =>
            {
                e.Handled = true;
                CloseTab(tab);
            };

            return tabButton;
        }

        private void UpdateTabButton(BrowserTab tab)
        {
            if (tab.TabButton?.Content is Grid grid)
            {
                var titleBlock = grid.Children.OfType<TextBlock>().FirstOrDefault();
                if (titleBlock != null)
                {
                    titleBlock.Text = FormatTabTitle(tab);
                }
            }
        }

        public void ActivateTab(BrowserTab tab)
        {
            if (_activeTab == tab) return;

            // Deactivate current tab
            if (_activeTab != null)
            {
                _activeTab.IsActive = false;
                _activeTab.TabButton?.Classes.Remove("active");
            }

            // Activate new tab
            _activeTab = tab;
            tab.IsActive = true;
            tab.TabButton?.Classes.Add("active");

            // Update UI
            _welcomeScreen.IsVisible = false;
            _browserWrapper.Child = tab.Browser;
            _addressBar.Text = tab.Url ?? string.Empty;
            Title = string.IsNullOrEmpty(tab.Title) ? Properties.Resources.BrowserToolDisplayName : $"{tab.Title} - {Properties.Resources.BrowserToolDisplayName}";

            // Navigate if URL is set and browser hasn't navigated yet
            if (!string.IsNullOrEmpty(tab.Url) && tab.Browser.Address != tab.Url)
            {
                tab.Browser.Address = tab.Url;
            }
        }

        private void CloseTab(BrowserTab tab)
        {
            if (_tabs.Count <= 1)
            {
                // If this is the last tab, show welcome screen
                _tabs.Remove(tab);
                if (tab.TabButton != null)
                {
                    _tabContainer.Children.Remove(tab.TabButton);
                }
                _activeTab = null;
                ShowWelcomeScreen();
                return;
            }

            var tabIndex = _tabs.IndexOf(tab);
            _tabs.Remove(tab);
            if (tab.TabButton != null)
            {
                _tabContainer.Children.Remove(tab.TabButton);
            }

            // If we're closing the active tab, activate another one
            if (_activeTab == tab)
            {
                var newActiveTab = tabIndex < _tabs.Count ? _tabs[tabIndex] : _tabs[tabIndex - 1];
                ActivateTab(newActiveTab);
            }

            // Dispose browser resources
            tab.Browser?.Dispose();
        }

        private void OnTabLoadStart(BrowserTab tab, Xilium.CefGlue.Common.Events.LoadStartEventArgs e)
        {
            tab.IsLoading = true;
            Log.Debug("Tab {TabId} started loading: {Url}", tab.Id, e.Frame?.Url ?? "unknown");
        }

        private async void OnTabLoadEnd(BrowserTab tab, Xilium.CefGlue.Common.Events.LoadEndEventArgs e)
        {
            tab.IsLoading = false;
            
            if (tab.Browser?.CefBrowser != null)
            {
                tab.Browser.CefBrowser.GetHost().SetAccessibilityState(CefState.Enabled);
                var f = e.Frame;
                if (f != null)
                {
                    var taskCompletionSource = new TaskCompletionSource<string>();

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try
                        {
                            f.VisitDom(new DOMVisitor(taskCompletionSource));
                        }
                        catch (Exception ex)
                        {
                            taskCompletionSource.SetResult(string.Format(Properties.Resources.Error_RetrievingPageSource, ex.Message));
                        }
                    });

                    await taskCompletionSource.Task;
                }
            }
        }

        private void OnTabAddressChanged(BrowserTab tab, string address)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                tab.UpdateUrl(address);
                if (tab.IsActive)
                {
                    _addressBar.Text = address;
                }
                UpdateTabButton(tab);
            });
        }

        private void OnTabTitleChanged(BrowserTab tab, string title)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                tab.UpdateTitle(title);
                if (tab.IsActive)
                {
                    Title = $"{tab.Title} - {Properties.Resources.BrowserToolDisplayName}";
                }
                UpdateTabButton(tab);
            });
        }

        private void NewTabButton_Click(object? sender, RoutedEventArgs e)
        {
            var tab = CreateNewTab();
            ActivateTab(tab);
        }

        private void BackButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_activeTab?.Browser?.CanGoBack == true)
                _activeTab.Browser.GoBack();
        }

        private void ForwardButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_activeTab?.Browser?.CanGoForward == true)
                _activeTab.Browser.GoForward();
        }

        private void RefreshButton_Click(object? sender, RoutedEventArgs e)
        {
            _activeTab?.Browser?.Reload();
        }

        private void DevToolsButton_Click(object? sender, RoutedEventArgs e)
        {
            _activeTab?.Browser?.ShowDeveloperTools();
        }

        private void AddressBar_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                NavigateToUrl(_addressBar.Text ?? string.Empty);
            }
        }

        private void GoButton_Click(object? sender, RoutedEventArgs e)
        {
            NavigateToUrl(_addressBar.Text ?? string.Empty);
        }

        public void NavigateToUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // Create a new tab if none exists
            if (_activeTab == null)
            {
                var tab = CreateNewTab();
                ActivateTab(tab);
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                // Check if it looks like a search query
                if (!url.Contains('.') || url.Contains(' '))
                {
                    url = $"https://www.google.com/search?q={Uri.EscapeDataString(url)}";
                }
                else
                {
                    url = "https://" + url;
                }
            }

            if (_activeTab != null)
            {
                _activeTab.UpdateUrl(url);
                _activeTab.Browser.Address = url;
            }
        }

        public async Task<string> GetPageSource()
        {
            if (_activeTab?.Browser == null)
                return Properties.Resources.NoActiveTab;
                
            return await GetPageSourceByVisitor();
        }

        private string CleanHtml(string html)
        {
            try
            {
                var parser = new HtmlParser();
                var document = parser.ParseDocument(html);

                // Remove non-content elements that don't typically contain readable text
                foreach (var element in document.QuerySelectorAll("script, style, meta, link, noscript, svg, canvas").ToArray())
                {
                    element.Remove();
                }

                // Convert non-readable elements to their text content
                foreach (var element in document.QuerySelectorAll("iframe, video, audio, img").ToArray())
                {
                    // Preserve alt text for images if available
                    if (element.LocalName == "img" && element.HasAttribute("alt") && !string.IsNullOrWhiteSpace(element.GetAttribute("alt")))
                    {
                        var textNode = document.CreateTextNode("[Image: " + element.GetAttribute("alt") + "]");
                        element.Parent?.ReplaceChild(textNode, element);
                    }
                    else
                    {
                        element.Remove();
                    }
                }

                // Remove hidden elements
                foreach (var element in document.QuerySelectorAll("[hidden], [style*='display: none'], [style*='display:none'], [style*='visibility: hidden'], [style*='visibility:hidden']").ToArray())
                {
                    element.Remove();
                }

                // Clean attributes but keep the elements and their structure
                foreach (var element in document.QuerySelectorAll("*").ToArray())
                {
                    var attributes = element.Attributes.ToArray();
                    foreach (var attr in attributes)
                    {
                        // Keep only essential attributes for understanding the structure
                        if (attr.Name != "id" && attr.Name != "class" && attr.Name != "title" && attr.Name != "alt" && attr.Name != "href" && attr.Name != "src")
                        {
                            element.RemoveAttribute(attr.Name);
                        }
                    }
                }

                // Generate the cleaned HTML with preserved structure
                var cleanedHtml = document.DocumentElement.OuterHtml;

                return cleanedHtml;
            }
            catch (Exception ex)
            {
                // If cleaning fails, return the original HTML
                return $"Error cleaning HTML: {ex.Message}";
            }
        }

        public async Task<string> GetPageSourceByVisitor()
        {
            if (_activeTab?.Browser?.CefBrowser == null)
                return "No active tab or browser";

            var taskCompletionSource = new TaskCompletionSource<string>();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var f = _activeTab.Browser.CefBrowser.GetMainFrame();
                    f.VisitDom(new DOMVisitor(taskCompletionSource));
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetResult($"Error retrieving page source: {ex.Message}");
                }
            });

            string html = await taskCompletionSource.Task;
            return CleanHtml(html);
        }

        public async Task<string> ConvertToPdf()
        {
            if (_activeTab?.Browser?.CefBrowser == null)
                return "No active tab or browser";
                
            var taskCompletionSource = new TaskCompletionSource<string>();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var host = _activeTab.Browser.CefBrowser.GetHost();
                    host.SetAccessibilityState(CefState.Enabled);
                    var settings = new CefPdfPrintSettings();
                    
                    // Get browser bounds for page size
                    var bounds = _activeTab.Browser.Bounds;
                    settings.Landscape = false;
                    settings.PrintBackground = false;
                    settings.DisplayHeaderFooter = false;
                    settings.PaperWidth = (int)bounds.Width+100;
                    settings.PaperWidth = (int)bounds.Height+100;
                    settings.MarginType = CefPdfPrintMarginType.Custom;
                    settings.MarginBottom = 0.5;
                    settings.MarginTop = 0.5;
                    settings.MarginLeft = 0.5;
                    settings.MarginRight = 0.5;
                    
                    // Generate PDF file path in temp directory
                    string pdfPath = Path.Combine(Path.GetTempPath(), $"webpage_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                    
                    host.PrintToPdf(pdfPath, settings, new PdfPrintCallback(taskCompletionSource));
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetResult(string.Format(Properties.Resources.Error_GeneratingPDF, ex.Message));
                }
            });
            
            return await taskCompletionSource.Task;
        }


        public BrowserTab? FindTabBySessionId(long sessionId)
        {
            return _tabs.FirstOrDefault(t => t.SessionId == sessionId);
        }

        /// <summary>
        /// Gets all tabs in the browser window
        /// </summary>
        /// <returns>Read-only collection of all tabs</returns>
        public IReadOnlyList<BrowserTab> GetAllTabs()
        {
            return _tabs.AsReadOnly();
        }



        public string GetActiveTabUrl()
        {
            return _activeTab?.Url ?? "";
        }



        public bool CanGoBack()
        {
            return _activeTab?.Browser?.CanGoBack == true;
        }

        public bool CanGoForward()
        {
            return _activeTab?.Browser?.CanGoForward == true;
        }

        public void GoBack()
        {
            if (CanGoBack())
            {
                _activeTab?.Browser?.GoBack();
            }
        }

        public void GoForward()
        {
            if (CanGoForward())
            {
                _activeTab?.Browser?.GoForward();
            }
        }

        /// <summary>
        /// Gets the currently active tab
        /// </summary>
        /// <returns>The active tab, or null if no tab is active</returns>
        public BrowserTab? GetActiveTab()
        {
            return _activeTab;
        }

        /// <summary>
        /// Closes all tabs that belong to the specified session
        /// </summary>
        /// <param name="sessionId">The session ID of tabs to close</param>
        public void CloseTabsBySessionId(long sessionId)
        {
            // Find all tabs with the matching session ID
            var tabsToClose = _tabs.Where(tab => tab.SessionId == sessionId).ToList();
            
            foreach (var tab in tabsToClose)
            {
                CloseTab(tab);
            }
        }

    }
}