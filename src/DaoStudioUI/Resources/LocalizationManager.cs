using System;
using System.Globalization;

namespace DaoStudioUI.Resources
{
    public static class LocalizationManager
    {
        public static event EventHandler? LanguageChanged;

        public static void SetLanguage(string cultureName)
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            
            // Notify all subscribers that the language has changed
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
        
        public static string GetCurrentLanguage()
        {
            return CultureInfo.CurrentUICulture.Name;
        }
        
        public static void SetDefaultLanguage()
        {
            SetLanguage("en-US");
        }
    }
} 