using DaoStudioUI.Models;
using Avalonia.Threading;
using DaoStudio;
using DaoStudio.Common.Plugins;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaoStudio;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;

namespace DaoStudioUI.ViewModels;

public partial class ChatViewModel
{
    /// <summary>
    /// Loads available tools from the business layer for UI selection
    /// </summary>
    private async Task LoadToolsAsync()
    {
        try
        {
            Log.Information("Loading tools for UI selection in session: {SessionId}", Session.Id);

            if (ToolsPanelViewModel != null)
            {
                // Clean up existing event subscriptions
                ClearToolEventSubscriptions();
                ToolsPanelViewModel.AvailableTools.Clear();
            }

            // Get enabled tools from the business layer (Session.Tools.cs)
            var enabledPersonTools = await Session.GetEnabledPersonToolsAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
          {
              foreach (var tool in enabledPersonTools)
              {
                  // Cast ITool to Tool to access full properties
                  var fullTool = tool;
                  var toolItem = new ToolItem(this)
                  {
                      Name = tool.Name,
                      Description = tool.Description,
                      PluginName = tool.StaticId,
                      Id = tool.Id,
                      StaticId = tool.StaticId,
                      ToolConfig = fullTool?.ToolConfig ?? string.Empty,
                      ToolType = fullTool?.ToolType ?? ToolType.Normal,
                      Parameters = fullTool?.Parameters ?? new Dictionary<string, string>(),
                      IsEnabled = tool.IsEnabled,
                      State = fullTool?.State ?? ToolState.Stateless,
                      LastModified = fullTool?.LastModified ?? DateTime.UtcNow,
                      IsSelected = true // By default, select all available tools
                  };

                  // Subscribe to selection changes
                  toolItem.SelectionChanged += OnToolItemSelectionChanged;

                  if (ToolsPanelViewModel != null)
                  {
                      ToolsPanelViewModel.AvailableTools.Add(toolItem);
                  }
              }

              Log.Debug("Loaded {ToolCount} tools for UI selection in session {SessionId}",
                  ToolsPanelViewModel?.AvailableTools.Count ?? 0, Session.Id);
          });

            // Apply initial UI selection to override business layer tools
            await ApplyToolSelectionAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load tools for UI selection");
        }
    }

    /// <summary>
    /// Applies the UI tool selection to override the business layer's automatic tool management
    /// </summary>
    private async Task ApplyToolSelectionAsync()
    {
        try
        {
            Log.Information("Applying UI tool selection to override business layer tools for session: {SessionId}", Session.Id);

            if (ToolsPanelViewModel == null)
            {
                Log.Warning("ToolsPanelViewModel is null, cannot apply tool selection");
                return;
            }

            // Get selected tools from UI
            var selectedTools = ToolsPanelViewModel.AvailableTools
                .Where(t => t.IsSelected)
                .ToList();

            if (!selectedTools.Any())
            {
                // If no tools are selected, clear all tools
                Session.SetTools(new Dictionary<string, List<FunctionWithDescription>>());
                Log.Information("No tools selected, cleared all tools from session {SessionId}", Session.Id);
                return;
            }            // Get the selected tool IDs for filtering
            var selectedToolIds = new HashSet<long>(selectedTools.Select(t => t.Id));

            // Get the current tools and their functions from the business layer
            var currentTools = Session.GetTools() ?? new Dictionary<string, List<FunctionWithDescription>>();
            var moduleFunctions = new Dictionary<string, List<FunctionWithDescription>>();

            // Filter the business layer tools to only include selected ones
            foreach (var moduleEntry in currentTools)
            {
                var filteredFunctions = new List<FunctionWithDescription>();

                foreach (var function in moduleEntry.Value)
                {
                    // Check if this function belongs to a selected tool
                    // We can match by tool name in the module key
                    var matchingTool = selectedTools.FirstOrDefault(t =>
                        moduleEntry.Key.Equals(t.Name, StringComparison.OrdinalIgnoreCase));

                    if (matchingTool != null)
                    {
                        filteredFunctions.Add(function);
                    }
                }

                if (filteredFunctions.Any())
                {
                    moduleFunctions[moduleEntry.Key] = filteredFunctions;
                }
            }

            // Override the business layer's tool selection
            Session.SetTools(moduleFunctions);

            Log.Information("Applied UI tool selection to session {SessionId}: {SelectedToolCount} tools with {FunctionCount} functions",
                Session.Id, selectedTools.Count, moduleFunctions.Values.Sum(f => f.Count));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply UI tool selection to session");
        }
    }    /// <summary>
         /// Called when tool selection changes in the UI to update the session
         /// </summary>
    public async Task OnToolSelectionChangedAsync()
    {
        await ApplyToolSelectionAsync();
    }

    /// <summary>
    /// Event handler for when individual tool selection changes
    /// </summary>
    private async void OnToolItemSelectionChanged(object? sender, EventArgs e)
    {
        try
        {
            // Trigger tool selection update asynchronously
            await OnToolSelectionChangedAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle tool selection change");
        }
    }

    /// <summary>
    /// Clears tool event subscriptions to prevent memory leaks
    /// </summary>
    private void ClearToolEventSubscriptions()
    {
        if (ToolsPanelViewModel?.AvailableTools != null)
        {
            foreach (var tool in ToolsPanelViewModel.AvailableTools)
            {
                tool.SelectionChanged -= OnToolItemSelectionChanged;
            }
        }
    }
}