using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Naming
{
    /// <summary>
    /// ViewModel wrapper for ParameterConfig to support data binding
    /// </summary>
    internal class ParameterConfigViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private bool _isRequired = true;
        private ParameterType _type = ParameterType.String;
        private ParameterConfig? _arrayElementConfig;
        private List<ParameterConfig>? _objectProperties;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool IsRequired
        {
            get => _isRequired;
            set => SetProperty(ref _isRequired, value);
        }
        
        public ParameterType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }
        
        public ParameterConfig? ArrayElementConfig
        {
            get => _arrayElementConfig;
            set => SetProperty(ref _arrayElementConfig, value);
        }
        
        public List<ParameterConfig>? ObjectProperties
        {
            get => _objectProperties;
            set => SetProperty(ref _objectProperties, value);
        }

        public ParameterConfigViewModel(ParameterConfig config)
        {
            Name = config.Name;
            Description = config.Description;
            IsRequired = config.IsRequired;
            Type = config.Type;
            ArrayElementConfig = config.ArrayElementConfig;
            ObjectProperties = config.ObjectProperties;
        }

        public ParameterConfig ToParameterConfig()
        {
            return new ParameterConfig
            {
                Name = Name,
                Description = Description,
                IsRequired = IsRequired,
                Type = Type,
                ArrayElementConfig = ArrayElementConfig,
                ObjectProperties = ObjectProperties
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}