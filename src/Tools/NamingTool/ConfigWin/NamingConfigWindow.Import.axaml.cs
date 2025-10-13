using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DaoStudio.Common.Plugins;
using FluentAvalonia.UI.Controls;
using MessagePack;
using MessagePack.Resolvers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Naming
{
    /// <summary>
    /// Configuration window for the Naming plugin
    /// </summary>
    internal partial class NamingConfigWindow : Window, INotifyPropertyChanged
    {
        
        /// <summary>
        /// Export current configuration to a MessagePack file
        /// </summary>
        private async void OnExportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use the StorageProvider API to save a file
                var storageProvider = this.StorageProvider;
                var fileName = $"naming_config_{DateTime.Now:yyyyMMdd}.dsnaming";
                
                var fileTypes = new FilePickerFileType("MessagePack Files")
                {
                    Patterns = new[] { "*.dsnaming" },
                    MimeTypes = new[] { "application/octet-stream" }
                };
                
                var filePickerOptions = new FilePickerSaveOptions
                {
                    Title = "Export Configuration",
                    FileTypeChoices = new[] { fileTypes },
                    DefaultExtension = "dsnaming",
                    SuggestedFileName = fileName
                };

                // Show the dialog and get the selected file
                var file = await storageProvider.SaveFilePickerAsync(filePickerOptions);
                if (file == null)
                {
                    // User cancelled
                    return;
                }

                // Create a config object with current settings
                var configToExport = CreateConfigFromCurrentSettings();
                
                var exportData = new Dictionary<string, object>
                {
                    { "metadata", new Dictionary<string, object>
                        {
                            { "exportDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                            { "exportedBy", "DaoStudio Naming Tool" },
                            { "version", "1.0" }
                        }
                    },
                    { "DaoStudioNamingTool", configToExport }
                };
                
                // Serialize the config to MessagePack using the contractless resolver
                var options = ContractlessStandardResolver.Options;
                byte[] messagePackData = MessagePackSerializer.Serialize(exportData, options);
                
                // Write to the file using the storage file API
                using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(messagePackData, 0, messagePackData.Length);
                
                // Show success message using the result dialog
                await ShowResultDialog("Configuration exported successfully!", this);
            }
            catch (Exception ex)
            {
                // Handle and show error message using the result dialog
                await ShowResultDialog($"Export Error: {ex.Message}", this);
            }
        }

        /// <summary>
        /// Import configuration from a MessagePack file
        /// </summary>
        private async void OnImportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Use the StorageProvider API to open a file
                var storageProvider = this.StorageProvider;

                var messagePackFileType = new FilePickerFileType("MessagePack Files")
                {
                    Patterns = new[] { "*.dsnaming" },
                    MimeTypes = new[] { "application/octet-stream" }
                };

                var filePickerOptions = new FilePickerOpenOptions
                {
                    Title = "Import Configuration",
                    FileTypeFilter = new[] { messagePackFileType },
                    AllowMultiple = false
                };

                // Show the dialog and get the selected file
                var files = await storageProvider.OpenFilePickerAsync(filePickerOptions);
                if (files == null || files.Count == 0)
                {
                    // User cancelled
                    return;
                }

                var file = files[0];
                
                // Handle MessagePack import
                await ImportMessagePackFile(file);
                
                // Show success message using the result dialog
                await ShowResultDialog("Configuration imported successfully!", this);
            }
            catch (Exception ex)
            {
                // Handle and show error message using the result dialog
                await ShowResultDialog($"Import Error: {ex.Message}", this);
            }
        }

        /// <summary>
        /// Import configuration from a MessagePack file
        /// </summary>
        private async Task ImportMessagePackFile(IStorageFile file)
        {
            using var stream = await file.OpenReadAsync();
            
            // Create a memory stream to read all bytes
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            byte[] messagePackData = memoryStream.ToArray();
            
            // Use the contractless resolver for MessagePack deserialization
            var options = ContractlessStandardResolver.Options;
            
            try
            {
                // Try to deserialize as a wrapper Dictionary first
                var importData = MessagePackSerializer.Deserialize<Dictionary<string, object>>(messagePackData, options);
                
                if (importData.TryGetValue("DaoStudioNamingTool", out var configObject))
                {
                    // Extract the configuration part and re-serialize it to MessagePack
                    byte[] configBytes = MessagePackSerializer.Serialize(configObject, options);
                    
                    // Load the configuration
                    LoadConfiguration(null, configBytes);
                }
            }
            catch
            {
                throw;
            }
        }

    }
}