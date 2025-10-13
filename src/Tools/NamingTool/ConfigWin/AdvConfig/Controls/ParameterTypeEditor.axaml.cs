using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Naming.AdvConfig.Controls
{
    /// <summary>
    /// Advanced parameter type editor control for complex parameter configuration
    /// </summary>
    internal partial class ParameterTypeEditor : UserControl, INotifyPropertyChanged
    {
        private ParameterConfig _parameter = new();
        private bool _isInitializing = false;

        public event EventHandler<ParameterConfig>? ParameterChanged;
        public event EventHandler? RemoveRequested;

        /// <summary>
        /// Avalonia Property for Parameter binding
        /// </summary>
        public static readonly StyledProperty<ParameterConfig?> ParameterProperty =
            AvaloniaProperty.Register<ParameterTypeEditor, ParameterConfig?>(nameof(Parameter));

        public static readonly StyledProperty<bool> ShowBasicInfoProperty =
            AvaloniaProperty.Register<ParameterTypeEditor, bool>(nameof(ShowBasicInfo), true);

        static ParameterTypeEditor()
        {
            ParameterProperty.Changed.AddClassHandler<ParameterTypeEditor>(OnParameterChanged);
        }

        /// <summary>
        /// Called when the parameter property changes
        /// </summary>
        private static void OnParameterChanged(ParameterTypeEditor editor, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is ParameterConfig parameter)
            {
                editor._parameter = parameter;
                editor.LoadParameter(parameter);
            }
        }

        public ParameterTypeEditor()
        {
            InitializeComponent();

            InitializeEventHandlers();
            UpdateTypeSpecificUI();
        }

        public ParameterTypeEditor(ParameterConfig parameter) : this()
        {
            LoadParameter(parameter);
        }

        #region Properties

        /// <summary>
        /// The parameter configuration being edited
        /// </summary>
        public ParameterConfig? Parameter
        {
            get => GetValue(ParameterProperty);
            set => SetValue(ParameterProperty, value);
        }

        public bool ShowBasicInfo
        {
            get => GetValue(ShowBasicInfoProperty);
            set => SetValue(ShowBasicInfoProperty, value);
        }

        /// <summary>
        /// Parameter name
        /// </summary>
        public string ParameterName
        {
            get => _parameter.Name;
            set
            {
                if (_parameter.Name != value)
                {
                    _parameter.Name = value;
                    OnPropertyChanged();
                    RaiseParameterChanged();
                }
            }
        }

        /// <summary>
        /// Parameter description
        /// </summary>
        public string ParameterDescription
        {
            get => _parameter.Description;
            set
            {
                if (_parameter.Description != value)
                {
                    _parameter.Description = value;
                    OnPropertyChanged();
                    RaiseParameterChanged();
                }
            }
        }

        /// <summary>
        /// Whether the parameter is required
        /// </summary>
        public bool IsRequired
        {
            get => _parameter.IsRequired;
            set
            {
                if (_parameter.IsRequired != value)
                {
                    _parameter.IsRequired = value;
                    OnPropertyChanged();
                    RaiseParameterChanged();
                }
            }
        }

        /// <summary>
        /// Parameter type
        /// </summary>
        public ParameterType ParameterType
        {
            get => _parameter.Type;
            set
            {
                if (_parameter.Type != value)
                {
                    _parameter.Type = value;
                    OnPropertyChanged();
                    UpdateTypeSpecificUI();
                    RaiseParameterChanged();
                }
            }
        }

        #endregion

        #region Initialization

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeEventHandlers()
        {
            // Get controls
            var nameTextBox = this.FindControl<TextBox>("ParameterNameTextBox");
            var descriptionTextBox = this.FindControl<TextBox>("ParameterDescriptionTextBox");
            var typeComboBox = this.FindControl<ComboBox>("ParameterTypeComboBox");
            var requiredCheckBox = this.FindControl<CheckBox>("RequiredCheckBox");
            var removeButton = this.FindControl<Button>("RemoveParameterButton");
            var addPropertyButton = this.FindControl<Button>("AddPropertyButton");
            var arrayElementTypeComboBox = this.FindControl<ComboBox>("ArrayElementTypeComboBox");
            var arrayElementDescriptionTextBox = this.FindControl<TextBox>("ArrayElementDescriptionTextBox");

            // Wire up events
            if (nameTextBox != null)
                nameTextBox.TextChanged += OnNameTextChanged;
            if (descriptionTextBox != null)
                descriptionTextBox.TextChanged += OnDescriptionTextChanged;
            if (typeComboBox != null)
                typeComboBox.SelectionChanged += OnTypeSelectionChanged;
            if (requiredCheckBox != null)
                requiredCheckBox.IsCheckedChanged += OnRequiredChanged;
            if (removeButton != null)
                removeButton.Click += OnRemoveClick;
            if (addPropertyButton != null)
                addPropertyButton.Click += OnAddPropertyClick;
            if (arrayElementTypeComboBox != null)
                arrayElementTypeComboBox.SelectionChanged += OnArrayElementTypeChanged;
            if (arrayElementDescriptionTextBox != null)
                arrayElementDescriptionTextBox.TextChanged += OnArrayElementDescriptionTextChanged;

            // Set default selection
            if (typeComboBox != null && typeComboBox.SelectedIndex == -1)
            {
                typeComboBox.SelectedIndex = 0; // Default to String
            }
        }

        private void LoadParameter(ParameterConfig parameter)
        {
            _isInitializing = true;

            if (parameter == null)
            {
                _parameter = new ParameterConfig();
                return;
            }
            try
            {
                _parameter = parameter;


                // Update UI controls
                var nameTextBox = this.FindControl<TextBox>("ParameterNameTextBox");
                var descriptionTextBox = this.FindControl<TextBox>("ParameterDescriptionTextBox");
                var typeComboBox = this.FindControl<ComboBox>("ParameterTypeComboBox");
                var requiredCheckBox = this.FindControl<CheckBox>("RequiredCheckBox");

                if (nameTextBox != null)
                    nameTextBox.Text = parameter.Name;
                if (descriptionTextBox != null)
                    descriptionTextBox.Text = parameter.Description;
                if (requiredCheckBox != null)
                    requiredCheckBox.IsChecked = parameter.IsRequired;
                
                // Set type selection
                if (typeComboBox != null)
                {
                    var typeIndex = parameter.Type switch
                    {
                        ParameterType.String => 0,
                        ParameterType.Number => 1,
                        ParameterType.Bool => 2,
                        ParameterType.Object => 3,
                        ParameterType.Array => 4,
                        _ => 0
                    };
                    typeComboBox.SelectedIndex = typeIndex;
                }

                // Array element controls
                if (parameter.Type == ParameterType.Array)
                {
                    var arrayElementTypeComboBox = this.FindControl<ComboBox>("ArrayElementTypeComboBox");
                    var arrayElementDescTextBox = this.FindControl<TextBox>("ArrayElementDescriptionTextBox");

                    if (parameter.ArrayElementConfig != null)
                    {
                        if (arrayElementTypeComboBox != null)
                        {
                            var elementIndex = parameter.ArrayElementConfig.Type switch
                            {
                                ParameterType.String => 0,
                                ParameterType.Number => 1,
                                ParameterType.Bool => 2,
                                ParameterType.Object => 3,
                                _ => 0
                            };
                            arrayElementTypeComboBox.SelectedIndex = elementIndex;
                        }
                        if (arrayElementDescTextBox != null)
                        {
                            arrayElementDescTextBox.Text = parameter.ArrayElementConfig.Description;
                        }
                    }
                
                }

                UpdateTypeSpecificUI();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        #endregion

        #region Event Handlers

        private void OnNameTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_isInitializing && sender is TextBox textBox)
            {
                ParameterName = textBox.Text ?? string.Empty;
            }
        }

        private void OnArrayElementDescriptionTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_isInitializing && sender is TextBox textBox && _parameter.ArrayElementConfig != null)
            {
                _parameter.ArrayElementConfig.Description = textBox.Text ?? string.Empty;
                RaiseParameterChanged();
            }
        }

        private void OnDescriptionTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (!_isInitializing && sender is TextBox textBox)
            {
                ParameterDescription = textBox.Text ?? string.Empty;
            }
        }

        private void OnTypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing && sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                var typeString = item.Tag?.ToString();
                if (Enum.TryParse<ParameterType>(typeString, out var parameterType))
                {
                    ParameterType = parameterType;
                }
            }
        }

        private void OnRequiredChanged(object? sender, RoutedEventArgs e)
        {
            if (!_isInitializing && sender is CheckBox checkBox)
            {
                IsRequired = checkBox.IsChecked == true;
            }
        }

        private void OnRemoveClick(object? sender, RoutedEventArgs e)
        {
            RemoveRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnAddPropertyClick(object? sender, RoutedEventArgs e)
        {
            if (_parameter.Type == ParameterType.Object)
            {
                // Add a new property to the object
                _parameter.ObjectProperties ??= new List<ParameterConfig>();
                
                var propertyName = $"property_{_parameter.ObjectProperties.Count + 1}";
                _parameter.ObjectProperties.Add(new ParameterConfig
                {
                    Name = propertyName,
                    Description = "Object property",
                    Type = ParameterType.String,
                    IsRequired = false
                });

                UpdateObjectPropertiesUI();
                RaiseParameterChanged();
            }
        }

        private void OnArrayElementTypeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializing && sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                var typeString = item.Tag?.ToString();
                if (Enum.TryParse<ParameterType>(typeString, out var elementType))
                {
                    // Avoid overwriting existing element config if the type has not actually changed. This
                    // can happen because the SelectionChanged event fires during control initialization
                    // after we programmatically set SelectedIndex in LoadParameter().
                    if (_parameter.ArrayElementConfig != null && _parameter.ArrayElementConfig.Type == elementType)
                    {
                        // Type is unchanged – keep the existing (and possibly complex) configuration intact.
                        return;
                    }

                    // Either the element type changed or no config existed – create/reset the element
                    // configuration. If a previous description was entered by the user, preserve it so
                    // that a simple type change does not wipe that information.
                    var previousDescription = _parameter.ArrayElementConfig?.Description ?? "Array element";

                    _parameter.ArrayElementConfig = new ParameterConfig
                    {
                        Name = "element",
                        Description = previousDescription,
                        Type = elementType,
                        IsRequired = true
                    };
                    UpdateArrayElementUI();
                    RaiseParameterChanged();
                }
            }
        }

        #endregion

        #region UI Updates

        private void UpdateTypeSpecificUI()
        {
            var advancedPanel = this.FindControl<StackPanel>("AdvancedTypePanel");
            var arrayPanel = this.FindControl<Border>("ArrayConfigPanel");
            var objectPanel = this.FindControl<Border>("ObjectConfigPanel");

            if (advancedPanel == null || arrayPanel == null || objectPanel == null)
                return;

            // Show/hide advanced configuration based on type
            bool showAdvanced = _parameter.Type == ParameterType.Array || _parameter.Type == ParameterType.Object;
            advancedPanel.IsVisible = showAdvanced;

            // Show/hide specific configuration panels
            arrayPanel.IsVisible = _parameter.Type == ParameterType.Array;
            objectPanel.IsVisible = _parameter.Type == ParameterType.Object;

            if (_parameter.Type == ParameterType.Array)
            {
                UpdateArrayElementUI();
            }
            else if (_parameter.Type == ParameterType.Object)
            {
                UpdateObjectPropertiesUI();
            }
        }

        private void UpdateArrayElementUI()
        {
            var arrayConfigBorder = this.FindControl<Border>("ArrayConfigPanel");
            if (arrayConfigBorder?.Child is not StackPanel container)
                return;

            // Remove existing nested editors
            var toRemove = new List<Control>();
            foreach (var child in container.Children)
            {
                if (child is ParameterTypeEditor)
                    toRemove.Add(child);
            }
            foreach (var child in toRemove)
                container.Children.Remove(child);

            if (_parameter.ArrayElementConfig == null)
                return;

            // Add nested editor for complex types
            if (_parameter.ArrayElementConfig.Type == ParameterType.Object || _parameter.ArrayElementConfig.Type == ParameterType.Array)
            {
                var elementEditor = new ParameterTypeEditor(_parameter.ArrayElementConfig);
                if (_parameter.ArrayElementConfig.Type == ParameterType.Object)
                {
                    elementEditor.ShowBasicInfo = false;
                }
                elementEditor.ParameterChanged += (s, cfg) =>
                {
                    _parameter.ArrayElementConfig = cfg;
                    RaiseParameterChanged();
                };
                container.Children.Add(elementEditor);
            }
        }

        private void UpdateObjectPropertiesUI()
        {
            var propertiesPanel = this.FindControl<StackPanel>("ObjectPropertiesPanel");
            var noPropertiesMessage = this.FindControl<TextBlock>("NoPropertiesMessage");

            if (propertiesPanel == null || noPropertiesMessage == null)
                return;

            // Clear existing property editors (except the "no properties" message)
            var childrenToRemove = new List<Control>();
            foreach (var child in propertiesPanel.Children)
            {
                if (child != noPropertiesMessage)
                {
                    childrenToRemove.Add(child);
                }
            }
            foreach (var child in childrenToRemove)
            {
                propertiesPanel.Children.Remove(child);
            }

            // Show/hide no properties message
            bool hasProperties = _parameter.ObjectProperties?.Count > 0;
            noPropertiesMessage.IsVisible = !hasProperties;

            if (hasProperties && _parameter.ObjectProperties != null)
            {
                // Add property editors
                for (int i = 0; i < _parameter.ObjectProperties.Count; i++)
                {
                    var index = i; // Capture for closure
                    var property = _parameter.ObjectProperties[i];
                    var propertyEditor = new ParameterTypeEditor(property);
                    propertyEditor.ParameterChanged += (s, p) =>
                    {
                        // Update the property in the list
                        _parameter.ObjectProperties[index] = p;
                        RaiseParameterChanged();
                    };
                    propertyEditor.RemoveRequested += (s, e) =>
                    {
                        _parameter.ObjectProperties.RemoveAt(index);
                        UpdateObjectPropertiesUI();
                        RaiseParameterChanged();
                    };
                    propertiesPanel.Children.Add(propertyEditor);
                }
            }
        }

        #endregion

        #region Helper Methods

        private void RaiseParameterChanged()
        {
            if (!_isInitializing)
            {
                ParameterChanged?.Invoke(this, _parameter);
            }
        }

        /// <summary>
        /// Get the current parameter configuration
        /// </summary>
        public ParameterConfig GetParameter()
        {
            return _parameter;
        }

        /// <summary>
        /// Validate the parameter configuration
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(_parameter.Name))
            {
                errors.Add("Parameter name is required");
            }

            if (string.IsNullOrWhiteSpace(_parameter.Description))
            {
                errors.Add("Parameter description is required");
            }

            return errors.Count == 0;
        }

        #endregion

        #region INotifyPropertyChanged

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}