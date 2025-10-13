using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Controls.ApplicationLifetimes;
using MimeSharp;
using Serilog;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using DesktopUI.ViewModels.Home.Chat;

namespace DaoStudioUI.ViewModels;

public partial class ChatViewModel
{

    [ObservableProperty]
    private ObservableCollection<Attachment> _attachments = new();
    public bool HasAttachments => Attachments.Count > 0;


    [RelayCommand]
    private async Task UploadFileAsync()
    { 
    //{
    //    try
    //    {
    //        // Get the current window or fallback to main window if not set
    //        var topLevel = _currentWindow;
            
    //        if (topLevel == null && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    //        {
    //            topLevel = desktop.MainWindow;
    //        }
            
    //        if (topLevel != null)
    //        {
    //            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    //            {
    //                Title = "Select File",
    //                AllowMultiple = false,
    //                // Allow all file types
    //                FileTypeFilter = new[]
    //                {
    //                    new FilePickerFileType("All Files")
    //                    {
    //                        Patterns = new[] { "*.*" }
    //                    }
    //                }
    //            });
                
    //            if (files.Count > 0)
    //            {
    //                using var stream = await files[0].OpenReadAsync();
    //                using var memoryStream = new MemoryStream();
    //                await stream.CopyToAsync(memoryStream);
                    
    //                var fileData = memoryStream.ToArray();
    //                string fileName = Path.GetFileName(files[0].Name);
    //                string mimeType = DetermineMimeType(files[0].Name);
                    
    //                // Create a preview image based on file type
    //                Bitmap? preview = null;
    //                Models.MessageType messageType;
                    
    //                if (IsImageFile(files[0].Name))
    //                {
    //                    // For images, use the actual image as preview
    //                    memoryStream.Position = 0;
    //                    preview = new Bitmap(memoryStream);
    //                    messageType = MessageType.Image;
    //                }
    //                else if (IsAudioFile(files[0].Name))
    //                {
    //                    // For audio files, use audio icon
    //                    preview = CreateIconForFileType("audio");
    //                    messageType = MessageType.Audio;
    //                }
    //                else
    //                {
    //                    // For other files, use document icon
    //                    preview = CreateIconForFileType("document");
    //                    // Still using Image type, but could create a generic "File" type
    //                    messageType = MessageType.Image;
    //                }
                    
    //                var attachment = new Attachment
    //                {
    //                    Data = fileData,
    //                    MimeType = mimeType,
    //                    Description = fileName,
    //                    Preview = preview,
    //                    Type = messageType
    //                };
                    
    //                Attachments.Add(attachment);
    //            }
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Log.Error(ex, "Failed to upload file");
    //    }
    }
    
    [RelayCommand]
    private void RemoveAttachment(Attachment attachment)
    {
        Attachments.Remove(attachment);
    }
    
    // Helper method to determine MIME type based on file extension
    private string DetermineMimeType(string fileName)
    {
        return Mime.Lookup(fileName);
    }
    
    // Helper method to check if file is an image
    private bool IsImageFile(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => true,
            _ => false
        };
    }
    
    // Helper method to check if file is audio
    private bool IsAudioFile(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".wav" or ".mp3" or ".ogg" or ".m4a" => true,
            _ => false
        };
    }
    
    // Helper method to create an icon for different file types
    private Bitmap? CreateIconForFileType(string fileType)
    {
        try
        {
            // Create a simple colored bitmap as a placeholder for different file types
            int width = 64;
            int height = 64;
            
            // Define colors based on file type
            byte[] colorBytes = fileType switch
            {
                "audio" => new byte[] { 0, 0, 255, 255 }, // Blue for audio
                "document" => new byte[] { 0, 128, 0, 255 }, // Green for documents
                _ => new byte[] { 128, 128, 128, 255 } // Gray for unknown
            };
            
            // Alternative approach without using unsafe code
            using (var stream = new MemoryStream())
            {
                // Write a simple colored PNG
                using (var writeableBitmap = new WriteableBitmap(
                    new Avalonia.PixelSize(width, height),
                    new Avalonia.Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888))
                {
                    using (var lockedBuffer = writeableBitmap.Lock())
                    {
                        // Create a safe buffer for the entire bitmap
                        var pixelData = new byte[height * lockedBuffer.RowBytes];
                        
                        // Fill the buffer with our color
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int offset = y * lockedBuffer.RowBytes + x * 4;
                                pixelData[offset] = colorBytes[0];     // B (in BGRA)
                                pixelData[offset + 1] = colorBytes[1]; // G
                                pixelData[offset + 2] = colorBytes[2]; // R
                                pixelData[offset + 3] = colorBytes[3]; // A
                            }
                        }
                        
                        // Copy the data to the bitmap
                        System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, lockedBuffer.Address, pixelData.Length);
                    }
                    
                    // Save to PNG format that we can then load
                    writeableBitmap.Save(stream);
                }
                
                // Reset stream position and load as regular bitmap
                stream.Position = 0;
                return new Bitmap(stream);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create file type icon");
            return null;
        }
    }
} 