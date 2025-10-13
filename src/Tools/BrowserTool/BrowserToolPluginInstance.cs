using DaoStudio.Common.Plugins;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BrowserTool;

public class BrowserToolPluginInstance : BasePlugin<BrowserToolConfig>
{
    private BrowserToolEmbeded? _browserTool;
    private readonly BrowserToolPluginFactory _factory;

    public BrowserToolPluginInstance(BrowserToolPluginFactory factory, PlugToolInfo plugInstanceInfo) : base(plugInstanceInfo)
    {
        _factory = factory;
    }

    protected override BrowserToolConfig CreateDefaultConfiguration()
    {
        var browserInfo = _factory.GetDefaultBrowserPath();
        return new BrowserToolConfig()
        {
            BrowserToolType = BrowserToolType.Embeded,
            BrowserPath = browserInfo.Path,
            BrowserType = browserInfo.Type
        };
    }

    protected override async Task RegisterToolFunctionsAsync(List<FunctionWithDescription> toolcallFunctions,
        IHostPerson? person, IHostSession? hostSession)
    {
        // Reuse existing browser tool or create a new one if it doesn't exist
        if (_browserTool == null)
        {
            // Create the browser tool instance using the current configuration
            _browserTool = new BrowserToolEmbeded(_config, _factory.Host);
        }

        // Use the extension method to create functions from the browser tool methods
        var toolFunctions = IPluginExtensions.CreateFunctionsFromToolMethods(_browserTool, "Web");
        toolcallFunctions.AddRange(toolFunctions);
    }

    protected override async Task<byte[]?> OnSessionCloseAsync(IHostSession hostSession)
    {
        // If session awareness is enabled and we have a browser tool instance, close session tabs
        if (_config.EnableSessionAware && _browserTool != null)
        {
            try
            {
                _browserTool.CloseSessionTabs(hostSession.Id);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the session close operation
                Log.Error(ex, "Failed to close session tabs for session {SessionId}", hostSession.Id);
            }
        }

        // Call the base implementation
        return await base.OnSessionCloseAsync(hostSession);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Close all session tabs if session awareness is enabled and browser tool exists
            if (_config.EnableSessionAware && _browserTool != null)
            {
                try
                {
                    // Get all session IDs from the browser tool's session tabs and close them
                    // Note: This will close all session tabs managed by this plugin instance
                    var sessionIds = _browserTool.GetSessionIds();
                    foreach (var sessionId in sessionIds)
                    {
                        _browserTool.CloseSessionTabs(sessionId);
                    }
                    Log.Information("Cleaned up session tabs during plugin disposal");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to clean up session tabs during plugin disposal");
                }
            }

            // Clean up the browser tool when disposing
            if (_browserTool is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _browserTool = null;
        }
        base.Dispose(disposing);
    }
}
