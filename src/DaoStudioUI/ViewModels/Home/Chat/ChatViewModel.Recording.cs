using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using DaoStudioUI.Models;
using Serilog;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;
using DaoStudioUI.Resources;
using DesktopUI.Resources;
using DesktopUI.ViewModels.Home.Chat;

namespace DaoStudioUI.ViewModels;

public partial class ChatViewModel
{
    private CancellationTokenSource? _recordingCancellationToken;
    private Stopwatch? _recordingStopwatch;
    private Timer? _recordingTimer;
    [ObservableProperty]
    private string _recordingTime = Strings.Chat_RecordingInitialTime;

    [RelayCommand]
    private void ToggleVoiceRecording()
    {
        if (IsRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }
    
    [RelayCommand]
    private void StopRecording()
    {
        //if (!IsRecording) return;
        
        //_recordingCancellationToken?.Cancel();
        //_recordingCancellationToken = null;
        //_recordingStopwatch?.Stop();
        //_recordingTimer?.Dispose();
        //_recordingTimer = null;
        
        //IsRecording = false;
        
        //// In a real implementation, we would process the recorded audio here
        //// We're just simulating it with a placeholder
        
        //try
        //{
        //    // Create a placeholder bitmap for the audio preview by loading a simple audio icon
        //    // In a real app, you would use an actual icon from your assets
        //    var assembly = Assembly.GetExecutingAssembly();
        //    Bitmap? placeholderBitmap = null;
            
        //    using (var stream = assembly.GetManifestResourceStream("DaoStudioUI.Assets.logo.ico"))
        //    {
        //        if (stream != null)
        //        {
        //            placeholderBitmap = new Bitmap(stream);
        //        }
        //    }
            
        //    // If we couldn't load from resources, create a bitmap from a memory stream with a simple pattern
        //    if (placeholderBitmap == null)
        //    {
        //        // Create a simple 1x1 pixel bitmap as a fallback
        //        using (var ms = new MemoryStream())
        //        {
        //            // Write the simplest possible valid PNG (1x1 transparent pixel)
        //            byte[] pngHeader = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 0, 1, 0, 0, 5, 0, 1, 13, 10, 45, 180, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
        //            ms.Write(pngHeader, 0, pngHeader.Length);
        //            ms.Position = 0;
        //            placeholderBitmap = new Bitmap(ms);
        //        }
        //    }
            
        //    var audioAttachment = new Attachment
        //    {
        //        Data = new byte[1024], // Placeholder data
        //        MimeType = "audio/wav",
        //        Description = Strings.Chat_VoiceRecordingDescription,
        //        Type = Models.MessageType.Audio,
        //        Preview = placeholderBitmap
        //    };
            
        //    Attachments.Add(audioAttachment);
        //}
        //catch (Exception ex)
        //{
        //    Log.Error(ex, "Failed to create audio attachment");
        //}
    }

    private void StartRecording()
    {
        if (IsRecording) return;

        _recordingCancellationToken = new CancellationTokenSource();
        _recordingStopwatch = Stopwatch.StartNew();

        // Update recording time every second
        _recordingTimer = new Timer(_ =>
        {
            var elapsed = _recordingStopwatch.Elapsed;
            RecordingTime = $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        }, null, 0, 1000);

        IsRecording = true;

        // Here would be the actual code to start recording audio
        // We're just simulating for now
    }

    [RelayCommand]
    private void PlayAudio(Models.ChatMessage message)
    {
        //if (message.Type != Models.MessageType.Audio)
        //    return;
            
        //var binaryDataList = message.BinaryContents;
        //if (binaryDataList == null || !binaryDataList.Any())
        //    return;

        //// In a real implementation, we would play each audio file in sequence
        //// For now, just log it
        //foreach (var binaryData in binaryDataList)
        //{
        //    Log.Information("Playing audio message {MessageId} - {Name}", message.Id, binaryData.Name);
        //}
    }
} 