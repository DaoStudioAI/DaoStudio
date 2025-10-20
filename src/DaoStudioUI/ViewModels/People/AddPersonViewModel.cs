using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaoStudio.Interfaces;
using DesktopUI.Resources;
using FluentAvalonia.UI.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DaoStudioUI.ViewModels
{
    public partial class AddPersonViewModel : ObservableObject
    {
        // Injected dependencies
        private readonly IApiProviderService _apiProviderService;
        private readonly ICachedModelService _cachedModelService;
        private readonly IToolService _toolService;
        private readonly IPeopleService _peopleService;
        
        // Events for modal window interaction
        public event EventHandler? SaveRequested;
        public event EventHandler? CancelRequested;

        // Declaration for the partial method
        partial void OnModelPropertyChanged(string? propertyName);

        [ObservableProperty]
        private PersonItem _person;

        [ObservableProperty]
        private string _windowTitle = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Provider> _providers = new();

        [ObservableProperty]
        private Provider? _selectedProvider;

        [ObservableProperty]
        private ObservableCollection<ICachedModel> _cachedModels = new();

        [ObservableProperty]
        private ICachedModel? _selectedCachedModel;

        [ObservableProperty]
        private bool _isLoading = false;        [ObservableProperty]
        private bool _hasChanges = false;        // UseAllTools is now computed based on PersonItem properties using extension methods
        public bool UseAllTools
        {
            get 
            {
                // Default behavior: if parameter not present, treat as enabled (backward compatibility)
                if (!Person.Parameters.TryGetValue(PersonParameterNames.UseAllTools, out var value))
                    return true;
                if (bool.TryParse(value, out var boolValue))
                    return boolValue;
                // If value is malformed, default to enabled
                return true;
            }
            set 
            { 
                // Align with PersonExtensions: remove key when enabling all tools; set to "false" when disabling
                if (value)
                {
                    if (Person.Parameters.ContainsKey(PersonParameterNames.UseAllTools))
                        Person.Parameters.Remove(PersonParameterNames.UseAllTools);
                    // Clear selected tools when enabling all tools
                    Person.ToolNames = Array.Empty<string>();
                }
                else
                {
                    // Explicitly set to false to indicate selective tool usage
                    Person.Parameters[PersonParameterNames.UseAllTools] = "false";
                    // Enable all tools by default in the selection UI when switching to specific tool selection
                    foreach (var tool in AvailableTools)
                    {
                        tool.IsSelected = true;
                    }
                    // Update Person.ToolNames will be handled by OnToolItemSelectionChanged
                }
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        private bool _hasValidImage = false;
        
        // HttpHeaders and AdditionalContent for LLM Parameters
        [ObservableProperty]
        private string _httpHeaders = string.Empty;
        
        [ObservableProperty]
        private string _additionalContent = string.Empty;

        // Validation error messages
        [ObservableProperty]
        private string? _httpHeadersError;

        [ObservableProperty]
        private string? _additionalContentError;
        
        // Partial method to validate HTTP Headers when changed
        partial void OnHttpHeadersChanged(string value)
        {
            var validation = ValidateHttpHeaders(value);
            HttpHeadersError = validation.IsValid ? null : validation.ErrorMessage;
        }

        // Partial method to validate Additional Content when changed
        partial void OnAdditionalContentChanged(string value)
        {
            var validation = ValidateAdditionalContent(value);
            AdditionalContentError = validation.IsValid ? null : validation.ErrorMessage;
        }
        
        // Tool selection properties for inline tool selection
        [ObservableProperty]
        private ObservableCollection<SimpleToolItem> _availableTools = new();
        
        [ObservableProperty]
        private ObservableCollection<object> _selectedToolItems = new();
        
        // Flag to track if name was manually set by user or loaded from DB
        private bool IsNameUserSet = false;
        
        // Collection of unique catalog names for UI
        [ObservableProperty]
        private ObservableCollection<string> _catalogNames = new();
        
        // Property to determine if catalog selection should be shown
        [ObservableProperty]
        private bool _showCatalogSelection = false;
        
        // Currently selected catalog
        [ObservableProperty]
        private string _selectedCatalog = "All";
        
        // Method to update catalog names from models
        private void UpdateCatalogNames()
        {
            // Clear existing catalog names
            CatalogNames.Clear();
            
            // Add "All" option first
            CatalogNames.Add(Strings.People_AllCatalogs);
            
            // Extract unique catalog names from models and add them
            var uniqueCatalogs = CachedModels
                .Where(m => !string.IsNullOrEmpty(m.Catalog))
                .Select(m => m.Catalog)
                .Distinct()
                .OrderBy(c => c);
                
            foreach (var catalog in uniqueCatalogs)
            {
                CatalogNames.Add(catalog);
            }
            
            // Determine if catalog selection should be shown (when we have catalogs besides "All")
            ShowCatalogSelection = CatalogNames.Count > 1;
            
            // Reset to "All" when changing providers
            SelectedCatalog = Strings.People_AllCatalogs;
        }
        
        // Filtered models based on selected catalog
        public IEnumerable<ICachedModel> FilteredModels => 
            SelectedCatalog == Strings.People_AllCatalogs 
                ? CachedModels 
                : CachedModels.Where(m => m.Catalog == SelectedCatalog);
        
        // Whether there are filtered models to display
        public bool HasFilteredModels => FilteredModels.Any();
        
        partial void OnSelectedCatalogChanged(string value)
        {
            // Remember current selected model ID before catalog change
            var currentModelId = SelectedCachedModel?.ModelId;
            
            // First, force the UI to update FilteredModels collection
            OnPropertyChanged(nameof(FilteredModels));
            OnPropertyChanged(nameof(HasFilteredModels));
            
            // Reset the selection temporarily to force UI to update
            SelectedCachedModel = null;
            OnPropertyChanged(nameof(SelectedCachedModel));
            
            // If we had a selection, try to maintain it in the filtered view
            if (!string.IsNullOrEmpty(currentModelId))
            {
                // Delay slightly to let UI process the change
                Dispatcher.UIThread.Post(() => {
                    // Try to find the same model in the filtered list
                    var modelInFilteredView = FilteredModels.FirstOrDefault(m => m.ModelId == currentModelId);
                    
                    if (modelInFilteredView != null)
                    {
                        // Restore selection if model exists in filtered view
                        SelectedCachedModel = modelInFilteredView;
                    }
                    else if (FilteredModels.Any())
                    {
                        // If previously selected model isn't in this catalog, select first available
                        SelectedCachedModel = FilteredModels.First();
                        
                        // Update ModelId to match selected model
                        if (Person.ModelId != SelectedCachedModel.ModelId)
                        {
                            Person.ModelId = SelectedCachedModel.ModelId;
                            HasChanges = true;
                        }
                    }
                    
                    // Force the UI to know the selection changed
                    OnPropertyChanged(nameof(SelectedCachedModel));
                }, DispatcherPriority.Background);
            }
        }
        
        // Method to check if we have a valid image
        private void UpdateHasValidImage()
        {
            HasValidImage = Person.Image != null && Person.Image.Length > 0;
        }
        
        // Load HttpHeaders and AdditionalContent from Person.Parameters
        private void LoadHttpHeadersAndAdditionalContent()
        {
            if (Person.Parameters.TryGetValue(PersonParameterNames.HttpHeaders, out var httpHeaders))
            {
                HttpHeaders = httpHeaders;
            }
            
            if (Person.Parameters.TryGetValue(PersonParameterNames.AdditionalContent, out var additionalContent))
            {
                AdditionalContent = additionalContent;
            }
        }
        
        // Save HttpHeaders and AdditionalContent to Person.Parameters
        private void SaveHttpHeadersAndAdditionalContent()
        {
            if (!string.IsNullOrWhiteSpace(HttpHeaders))
            {
                Person.Parameters[PersonParameterNames.HttpHeaders] = HttpHeaders;
            }
            else
            {
                Person.Parameters.Remove(PersonParameterNames.HttpHeaders);
            }
            
            if (!string.IsNullOrWhiteSpace(AdditionalContent))
            {
                Person.Parameters[PersonParameterNames.AdditionalContent] = AdditionalContent;
            }
            else
            {
                Person.Parameters.Remove(PersonParameterNames.AdditionalContent);
            }
        }
        

        public AddPersonViewModel(IPerson? person, IApiProviderService apiProviderService, ICachedModelService cachedModelService,
         IToolService toolService, IPeopleService peopleService)
        {
            _apiProviderService = apiProviderService;
            _cachedModelService = cachedModelService;
            _toolService = toolService;
            _peopleService = peopleService;


            // Set model if provided
            if (person != null)
            {
                _person = PersonItem.FromIPerson(person);

                IsNameUserSet = !string.IsNullOrEmpty(Person.Name);

                // Check if the image is valid
                UpdateHasValidImage();
                
                // Load HttpHeaders and AdditionalContent from Person.Parameters
                LoadHttpHeadersAndAdditionalContent();
                
                // UseAllTools property is now computed from person parameters
                // Just trigger property change notification to update UI
                OnPropertyChanged(nameof(UseAllTools));
                
                // Set window title based on whether we're adding or editing
                if (person.Id != 0)
                {
                    // Format: "Edit {0}" where {0} is the model name
                    WindowTitle = string.Format(Strings.People_EditPersonTitleFormat, person.Name);
                }
                else
                {
                    WindowTitle = Strings.People_AddNewPerson;
                }
            }
            else
            {
                _person = new PersonItem();
                WindowTitle = Strings.People_AddNewPerson;
            } 
            // Load providers
            Task.Run(() => LoadProvidersAsync());
            
            // Load available tools
            Task.Run(() => LoadAvailableToolsAsync());
            
            // Listen for selected tool items changes
            SelectedToolItems.CollectionChanged += OnSelectedToolItemsChanged;
        }

        private async Task LoadProvidersAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            
            try
            {
                // Load providers
                var providers = await _apiProviderService.GetAllApiProvidersAsync();

                Dispatcher.UIThread.Post(() => {
                    Providers.Clear();
                    foreach (var provider in providers)
                    {
                        Providers.Add(Provider.FromApiProvider(provider));
                    }
                    
                    // If we have a model with a provider name, select that provider
                    if (!string.IsNullOrEmpty(Person.ProviderName))
                    {
                        SelectedProvider = Providers.FirstOrDefault(p => p.Name == Person.ProviderName);
                    }

                    // If no provider selected yet, select first provider if available
                    if (SelectedProvider == null && Providers.Count > 0)
                    {
                        SelectedProvider = Providers[0];
                    }

                    IsLoading = false;
                    
                    // Load cached models for the selected provider if available
                    if (SelectedProvider != null)
                    {
                        Task.Run(() => LoadCachedModelsAsync(SelectedProvider.Id));
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle errors
                Log.Error(ex, "Error loading providers");
                IsLoading = false;
            }
        }

        partial void OnSelectedProviderChanged(Provider? value)
        {
            if (value != null && value.Name != Person.ProviderName)
            {
                Person.ProviderName = value.Name;
                HasChanges = true;
                
                // Load cached models for this provider
                Task.Run(() => LoadCachedModelsAsync(value.Id));
            }
            else if (value != null && !IsLoading)
            {
                // Ensure cached models are loaded even when provider name matches
                // but only if we're not in the initial loading phase
                Task.Run(() => LoadCachedModelsAsync(value.Id));
            }
        }

        private async Task LoadCachedModelsAsync(long providerId)
        {
            if (IsLoading) return;
            
            IsLoading = true;
            
            try
            {
                // Load cached models for the selected provider using CachedModelService
                var allModels = await _cachedModelService.GetAllCachedModelsAsync();
                var models = allModels.Where(m => m.ApiProviderId == providerId);

                Dispatcher.UIThread.Post(() => {
                    // Clear and add as a batch operation to avoid multiple UI updates
                    var modelList = models.ToList();
                    CachedModels.Clear();
                    
                    // Add all models at once
                    foreach (var model in modelList)
                    {
                        CachedModels.Add(model);
                    }
                    
                    // Update catalog names for the UI before setting selected model
                    UpdateCatalogNames();
                    
                    // Ensure UI knows filtered collections have changed
                    OnPropertyChanged(nameof(FilteredModels));
                    OnPropertyChanged(nameof(HasFilteredModels));
                    
                    // Now set the selected model AFTER the filtered collections are updated
                    // Determine model to select (exact match) and/or infer catalog
                    ICachedModel? modelToSelect = GetModelToSelect(Person.ModelId);


                    //Users may input a provider name that doesn't exist in the list. In that case, we should not override it.
                    // If we couldn't find the model or didn't have a ModelId, select first available
                    //if (modelToSelect == null && CachedModels.Count > 0)
                    //{
                    //    modelToSelect = CachedModels.First();

                    //    // Update ModelId to match selected model
                    //    if (Person.ModelId != modelToSelect.ModelId)
                    //    {
                    //        Person.ModelId = modelToSelect.ModelId;
                    //        HasChanges = true;
                    //    }
                    //}


                    var inferredCatalog = TryInferCatalogForModel(Person.ModelId);
                    if (!string.IsNullOrEmpty(inferredCatalog))
                    {
                        SelectedCatalog = inferredCatalog;
                        OnPropertyChanged(nameof(FilteredModels));
                        OnPropertyChanged(nameof(HasFilteredModels));
                    }

                    // Set the selected model explicitly through property setter
                    if (modelToSelect != null)
                    {

                        SelectedCachedModel = modelToSelect;

                        // Force the UI to know the selection changed
                        OnPropertyChanged(nameof(SelectedCachedModel));
                    }
                    
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                // Handle errors
                Log.Error(ex, "Error loading cached models");
                IsLoading = false;
            }
        }

        partial void OnSelectedCachedModelChanged(ICachedModel? value)
        {
            if (value != null)
            {
                // Always update ModelId when selection changes to ensure UI stays in sync
                if (Person.ModelId != value.ModelId)
                {
                    Person.ModelId = value.ModelId;
                    HasChanges = true;
                    
                    // Explicitly update the modelId in the UI
                    OnPropertyChanged(nameof(Person));
                    
                    // When ModelId changes, log this for debugging
                    Log.Debug("Person selection changed to {ModelId} ({ModelName})", value.ModelId, value.Name);
                }

                // Update Name if it's not user-set (empty or loaded from DB)
                if (!IsNameUserSet || string.IsNullOrEmpty(Person.Name))
                {
                    Person.Name = value.Name;
                    IsNameUserSet = false;
                }
            }        }


        [RelayCommand]
        private async Task UploadImage()
        {
            try
            {
                // Get the main window
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var mainWindow = desktop.MainWindow;
                    
                    // Create file picker
                    var filePickerOptions = new FilePickerOpenOptions
                    {
                        Title = "Select Image",
                        AllowMultiple = false,
                        FileTypeFilter = new FilePickerFileType[]
                        {
                            new FilePickerFileType("Image Files")
                            {
                                Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" },
                                MimeTypes = new[] { "image/jpeg", "image/png", "image/bmp", "image/gif" }
                            }
                        }
                    };
                    
                    // Show file picker
                    var result = await mainWindow!.StorageProvider.OpenFilePickerAsync(filePickerOptions);
                    
                    // Process the selected file
                    if (result != null && result.Count > 0)
                    {
                        var file = result[0];
                        
                        // Read file as byte array
                        await using var stream = await file.OpenReadAsync();
                        using var memoryStream = new MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        
                        // Update model
                        Person.Image = memoryStream.ToArray();
                        HasChanges = true;
                        
                        // Update image validity
                        UpdateHasValidImage();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error uploading image");
            }
        }

        [RelayCommand]
        private void ClearImage()
        {
            Person.Image = null;
            HasChanges = true;
            
            // Update image validity
            UpdateHasValidImage();
        }


        private async Task LoadAvailableToolsAsync()
        {
            try
            {
                // Get all available tools using DaoStudio.Tools.cs
                var tools = await _toolService.GetAllToolsAsync();
                  Dispatcher.UIThread.Post(() => {
                    AvailableTools.Clear();
                    
                    foreach (var tool in tools)
                    {
                        var toolItem = new SimpleToolItem
                        {
                            Name = tool.Name,
                            Description = tool.Description ?? string.Empty,
                            PluginName = tool.StaticId ,
                            IsSelected = Person.ToolNames.Contains(tool.Name)
                        };
                        
                        // Subscribe to selection changes
                        toolItem.SelectionChanged += OnToolItemSelectionChanged;
                        
                        AvailableTools.Add(toolItem);
                    }
                    
                    // Update selected tool items collection
                    UpdateSelectedToolItemsFromPerson();
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading available tools");
            }
        }

        // Helper: find exact cached model match by modelId
        private ICachedModel? GetModelToSelect(string? personModelId)
        {
            if (string.IsNullOrEmpty(personModelId)) return null;
            return CachedModels.FirstOrDefault(m => m.ModelId == personModelId);
        }

        // Helper: try to infer a catalog for a person modelId when exact model isn't found
        private string? TryInferCatalogForModel(string? personModelId)
        {
            if (string.IsNullOrEmpty(personModelId)) return null;

            var candidates = CachedModels.Where(m => !string.IsNullOrEmpty(m.Catalog)).ToList();

            var candidate = candidates.FirstOrDefault(m => string.Equals(m.ModelId, personModelId, StringComparison.OrdinalIgnoreCase))
                           ?? candidates.FirstOrDefault(m => m.ModelId.EndsWith(personModelId, StringComparison.OrdinalIgnoreCase))
                           ?? candidates.FirstOrDefault(m => personModelId.EndsWith(m.ModelId, StringComparison.OrdinalIgnoreCase))
                           ?? candidates.FirstOrDefault(m => m.ModelId.IndexOf(personModelId, StringComparison.OrdinalIgnoreCase) >= 0)
                           ?? candidates.FirstOrDefault(m => personModelId.IndexOf(m.ModelId, StringComparison.OrdinalIgnoreCase) >= 0);

            return candidate?.Catalog;
        }

        private void OnSelectedToolItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Update Person.ToolNames based on selected tools
            UpdatePersonToolNamesFromSelection();
        }

        private void OnToolItemSelectionChanged(object? sender, EventArgs e)
        {
            // Update Person.ToolNames based on selected tools
            UpdatePersonToolNamesFromSelection();
        }

        private void UpdatePersonToolNamesFromSelection()
        {
            var selectedToolNames = AvailableTools
                .Where(t => t.IsSelected)
                .Select(t => t.Name)
                .ToArray();

            if (!Person.ToolNames.SequenceEqual(selectedToolNames))
            {
                Person.ToolNames = selectedToolNames;
                HasChanges = true;
            }
        }

        private void UpdateSelectedToolItemsFromPerson()
        {
            SelectedToolItems.Clear();
              foreach (var tool in AvailableTools.Where(t => t.IsSelected))
            {
                SelectedToolItems.Add(tool);
            }
        }

        [RelayCommand]
        private void SelectAllTools()
        {
            foreach (var tool in AvailableTools)
            {
                tool.IsSelected = true;
            }
        }

        [RelayCommand]
        private void ClearAllTools()
        {
            foreach (var tool in AvailableTools)
            {
                tool.IsSelected = false;
            }
        }

        /// <summary>
        /// Validates HTTP Headers format
        /// Expected format: One header per line, each in "Header-Name: Header-Value" format
        /// </summary>
        private ValidationResult ValidateHttpHeaders(string? httpHeaders)
        {
            // Empty or whitespace is valid (optional field)
            if (string.IsNullOrWhiteSpace(httpHeaders))
            {
                return new ValidationResult { IsValid = true };
            }

            try
            {
                var lines = httpHeaders.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var lineNumber = 0;

                foreach (var line in lines)
                {
                    lineNumber++;
                    var trimmedLine = line.Trim();
                    
                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        continue;
                    }

                    var colonIndex = trimmedLine.IndexOf(':');
                    
                    // Check if line contains a colon
                    if (colonIndex <= 0)
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"Line {lineNumber}: Missing colon separator. Expected format: 'Header-Name: Header-Value'\nLine content: {trimmedLine}"
                        };
                    }

                    var headerName = trimmedLine.Substring(0, colonIndex).Trim();
                    var headerValue = trimmedLine.Substring(colonIndex + 1).Trim();

                    // Validate header name is not empty
                    if (string.IsNullOrWhiteSpace(headerName))
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"Line {lineNumber}: Header name cannot be empty.\nLine content: {trimmedLine}"
                        };
                    }

                    // Validate header value is not empty
                    if (string.IsNullOrWhiteSpace(headerValue))
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"Line {lineNumber}: Header value cannot be empty for header '{headerName}'.\nLine content: {trimmedLine}"
                        };
                    }

                    // Validate header name doesn't contain invalid characters
                    if (headerName.Any(c => char.IsWhiteSpace(c) || c == ':'))
                    {
                        return new ValidationResult
                        {
                            IsValid = false,
                            ErrorMessage = $"Line {lineNumber}: Header name '{headerName}' contains invalid characters (whitespace or colon)."
                        };
                    }
                }

                return new ValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Error parsing HTTP headers: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Validates Additional Content format
        /// Expected format: Valid JSON that can be deserialized into Dictionary<string, object>
        /// </summary>
        private ValidationResult ValidateAdditionalContent(string? additionalContent)
        {
            // Empty or whitespace is valid (optional field)
            if (string.IsNullOrWhiteSpace(additionalContent))
            {
                return new ValidationResult { IsValid = true };
            }

            try
            {
                // Try to parse as JSON
                var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(additionalContent);
                
                if (parsed == null)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "JSON content is null or invalid. Expected a JSON object with key-value pairs."
                    };
                }

                return new ValidationResult { IsValid = true };
            }
            catch (System.Text.Json.JsonException ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Invalid JSON format: {ex.Message}\n\nExpected format: {{ \"key1\": \"value1\", \"key2\": \"value2\" }}"
                };
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Error parsing JSON: {ex.Message}"
                };
            }
        }



        [RelayCommand]
        private async Task Save()
        {
            // Validate model fields
            if (string.IsNullOrWhiteSpace(Person.Name))
            {
                // Show validation error message
                var dialog = new ContentDialog
                {
                    Title = Strings.Settings_Error,
                    Content = Strings.People_NameRequired,
                    PrimaryButtonText = Strings.Common_OK,
                };
                await dialog.ShowAsync();
                return;
            }

            // Validate provider
            if (string.IsNullOrWhiteSpace(Person.ProviderName))
            {
                // Show validation error message
                var dialog = new ContentDialog
                {
                    Title = Strings.Settings_Error,
                    Content = Strings.People_ProviderRequired,
                    PrimaryButtonText = Strings.Common_OK
                };
                await dialog.ShowAsync();
                return;
            }

            // Validate modelId
            if (string.IsNullOrWhiteSpace(Person.ModelId))
            {
                // Show validation error message
                var dialog = new ContentDialog
                {
                    Title = Strings.Settings_Error,
                    Content = Strings.People_PersonIdRequired,
                    PrimaryButtonText = Strings.Common_OK
                };
                await dialog.ShowAsync();
                return;
            }

            // Validate HTTP Headers format
            var httpHeadersValidation = ValidateHttpHeaders(HttpHeaders);
            if (!httpHeadersValidation.IsValid)
            {
                // Update the error property to show in UI
                HttpHeadersError = httpHeadersValidation.ErrorMessage;
                
                var dialog = new ContentDialog
                {
                    Title = Strings.Settings_Error,
                    Content = $"Invalid HTTP Headers format:\n{httpHeadersValidation.ErrorMessage}",
                    PrimaryButtonText = Strings.Common_OK
                };
                await dialog.ShowAsync();
                return;
            }

            // Validate Additional Content format
            var additionalContentValidation = ValidateAdditionalContent(AdditionalContent);
            if (!additionalContentValidation.IsValid)
            {
                // Update the error property to show in UI
                AdditionalContentError = additionalContentValidation.ErrorMessage;
                
                var dialog = new ContentDialog
                {
                    Title = Strings.Settings_Error,
                    Content = $"Invalid Additional Content format:\n{additionalContentValidation.ErrorMessage}",
                    PrimaryButtonText = Strings.Common_OK
                };
                await dialog.ShowAsync();
                return;
            }

            await SaveModelAsync();
            // Trigger the SaveRequested event to notify the parent window
            SaveRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void Cancel()
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        public async Task<IPerson?> SaveModelAsync()
        {
            try
            {
                // Save HttpHeaders and AdditionalContent to Person.Parameters before saving
                SaveHttpHeadersAndAdditionalContent();
                
                // Update existing IPerson if present; otherwise, create a new one
                if (Person.person == null)
                {
                    // Create new person using parameters from PersonItem
                    var created = await _peopleService.CreatePersonAsync(
                        Person.Name,
                        Person.Description,
                        Person.Image,
                        Person.IsEnabled,
                        Person.ProviderName,
                        Person.ModelId,
                        Person.DeveloperMessage,
                        Person.ToolNames,
                        Person.Parameters,
                        Person.PresencePenalty,
                        Person.FrequencyPenalty,
                        Person.TopP,
                        Person.TopK,
                        Person.Temperature
                    );

                    // Sync back state if created
                    Person.person = created;
                    if (created != null)
                    {
                        Person.Id = created.Id;
                        Person.LastModified = created.LastModified;
                        HasChanges = false;
                    }

                    return created;
                }
                else
                {
                    // Update existing person fields from PersonItem
                    var model = Person.person;
                    model.Name = Person.Name;
                    model.Description = Person.Description;
                    model.Image = Person.Image;
                    model.IsEnabled = Person.IsEnabled;
                    model.ProviderName = Person.ProviderName;
                    model.ModelId = Person.ModelId;
                    model.DeveloperMessage = Person.DeveloperMessage;
                    model.ToolNames = Person.ToolNames;
                    model.Parameters = Person.Parameters;
                    model.PresencePenalty = Person.PresencePenalty;
                    model.FrequencyPenalty = Person.FrequencyPenalty;
                    model.TopP = Person.TopP;
                    model.TopK = Person.TopK;
                    model.Temperature = Person.Temperature;
                    model.LastModified = DateTime.UtcNow;

                    var saved = await _peopleService.SavePersonAsync(model);

                    // Sync back state if saved
                    if (saved != null)
                    {
                        Person.person = saved;
                        Person.Id = saved.Id;
                        Person.LastModified = saved.LastModified;
                        HasChanges = false;
                    }

                    return saved;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving model");
                return null;
            }
        }

        // Public method to handle name changes from view
        public void HandleNameChanged()
        {
            if (!string.IsNullOrEmpty(Person.Name))
            {
                IsNameUserSet = true;   
                HasChanges = true;
            }
        }
    } 
    // Dummy ChatViewModel for ToolItemModel constructor - minimal implementation
    internal partial class DummyChatViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isLoading = false;
    }
      // Simple tool item for tool selection without ChatViewModel dependency
    public partial class SimpleToolItem : ObservableObject
    {
        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _pluginName = string.Empty;

        private bool _isSelected = false;
        public bool IsSelected 
        { 
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? SelectionChanged;
        
        public bool IsLoading => false; // Always false for simple implementation
    }

    /// <summary>
    /// Result of a validation operation
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}