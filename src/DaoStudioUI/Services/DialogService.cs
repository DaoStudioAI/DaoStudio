using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using DaoStudioUI.Resources;
using Serilog;
using DesktopUI.Resources;

namespace DaoStudioUI.Services
{
    /// <summary>
    /// Service for displaying dialog boxes in the application
    /// </summary>
    public static class DialogService
    {
        /// <summary>
        /// Shows an error dialog with the specified title and message
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The error message</param>
        /// <param name="parent">Optional parent window</param>
        /// <returns>A task that completes when the dialog is closed</returns>
        public static async Task ShowErrorAsync(string title, string message, Window? parent = null)
        {
            Log.Error("Showing error dialog. Title: {DialogTitle}, Message: {DialogMessage}", title, message);
            await ShowDialogAsync(title, message, Strings.Common_OK, null, null, parent);
        }

        /// <summary>
        /// Shows an error dialog for an exception
        /// </summary>
        /// <param name="ex">The exception to display</param>
        /// <param name="title">Optional title (defaults to "Error")</param>
        /// <param name="parent">Optional parent window</param>
        /// <returns>A task that completes when the dialog is closed</returns>
        public static async Task ShowExceptionAsync(Exception ex, string? title = null, Window? parent = null)
        {
            Log.Error(ex, "Error displayed to user: {Message}", ex.Message);
            await ShowErrorAsync(title ?? Strings.Settings_Error, ex.Message, parent);
        }

        /// <summary>
        /// Shows a dialog with customizable buttons and content
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="content">The dialog content</param>
        /// <param name="closeButtonText">Text for the close/cancel button</param>
        /// <param name="primaryButtonText">Optional text for the primary button</param>
        /// <param name="secondaryButtonText">Optional text for the secondary button</param>
        /// <param name="parent">Optional parent window</param>
        /// <returns>The dialog result</returns>
        public static async Task<ContentDialogResult> ShowDialogAsync(
            string title, 
            object content, 
            string closeButtonText,
            string? primaryButtonText = null,
            string? secondaryButtonText = null,
            Window? parent = null)
        {
            ContentDialogResult result = ContentDialogResult.None;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = title,
                        Content = content,
                        CloseButtonText = closeButtonText
                    };

                    if (!string.IsNullOrWhiteSpace(primaryButtonText))
                    {
                        dialog.PrimaryButtonText = primaryButtonText;
                        dialog.DefaultButton = ContentDialogButton.Primary;
                    }

                    if (!string.IsNullOrWhiteSpace(secondaryButtonText))
                    {
                        dialog.SecondaryButtonText = secondaryButtonText;
                    }

                    // Set parent window if provided to properly position dialog
                    if (parent != null)
                    {
                        // In Avalonia, we need to use the ShowAsync overload that takes a parent window
                        result = await dialog.ShowAsync(parent);
                    }
                    else
                    {
                        // Use the parameterless ShowAsync when no parent is provided
                        result = await dialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    // Log any errors that occur while showing dialog 
                    Log.Error(ex, "Error showing dialog");
                }
            });

            return result;
        }
    }
} 