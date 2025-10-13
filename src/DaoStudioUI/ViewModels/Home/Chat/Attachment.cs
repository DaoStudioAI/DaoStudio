using System;
using Avalonia.Media.Imaging;
using DaoStudioUI.Models;
using DaoStudio.Interfaces;

namespace DesktopUI.ViewModels.Home.Chat;

public class Attachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required byte[] Data { get; set; }
    public required string MimeType { get; set; }
    public required string Description { get; set; }
    public Bitmap? Preview { get; set; }
    public MessageType Type { get; set; }
} 