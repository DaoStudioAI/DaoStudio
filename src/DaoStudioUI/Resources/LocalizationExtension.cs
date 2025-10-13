using Avalonia.Data;
using Avalonia.Markup.Xaml;
using System;
using System.Resources;

namespace DaoStudioUI.Resources
{
    public class LocalizationExtension : MarkupExtension
    {
        private static readonly ResourceManager _resourceManager = new ResourceManager("DesktopUI.Resources.Strings", typeof(LocalizationExtension).Assembly);

        public string Key { get; set; }

        public LocalizationExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(Key))
                return string.Empty;

            string? localizedValue = _resourceManager.GetString(Key);

            if (localizedValue == null)
            {
                return new BindingNotification(new InvalidOperationException($"Key '{Key}' not found in resources."), BindingErrorType.Error);
            }

            return localizedValue;
        }
    }
} 