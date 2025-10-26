using System;
using System.Threading.Tasks;
using NetSparkleUpdater;
using NetSparkleUpdater.Events;
using NetSparkleUpdater.SignatureVerifiers;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.UI.Avalonia;
using Microsoft.Extensions.Logging;
using Avalonia.Controls;

namespace DaoStudioUI.Services
{
    public class UpdateService : IDisposable
    {
        private readonly ILogger<UpdateService> _logger;
        private SparkleUpdater? _sparkleUpdater;
        private bool _disposed = false;
        private string? _appCastUrl;

        public UpdateService(ILogger<UpdateService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// A lightweight projection of app cast data that our UI needs.
        /// </summary>
        public class AppcastSummary
        {
            public string? AppCastUrl { get; set; }
            public string? LatestVersion { get; set; }
            public DateTimeOffset? LatestPublishedOn { get; set; }
            public string? LatestReleaseNotesLink { get; set; }
            public DateTimeOffset? CurrentPublishedOn { get; set; }
        }

        /// <summary>
        /// Returns the configured app cast URL if available.
        /// </summary>
        public string? GetAppCastUrl() => _appCastUrl;

        /// <summary>
        /// Fetches the latest app cast information using NetSparkle and returns a simplified summary.
        /// Uses reflection to avoid compile-time dependency on NetSparkle's internal types.
        /// </summary>
        /// <param name="currentVersion">Current application version used to locate the matching item in the app cast for build time.</param>
        public async Task<AppcastSummary?> GetAppcastSummaryAsync(string currentVersion)
        {
            if (_sparkleUpdater == null)
            {
                _logger.LogWarning("UpdateService not initialized. Call Initialize() first.");
                return null;
            }

            try
            {
                // Quietly check for updates and read the app cast
                var updateInfo = await _sparkleUpdater.CheckForUpdatesQuietly();
                if (updateInfo == null)
                    return new AppcastSummary { AppCastUrl = _appCastUrl };

                var summary = new AppcastSummary { AppCastUrl = _appCastUrl };

                // updateInfo.Updates is a list of app cast items. Use reflection to read.
                var updatesProp = updateInfo.GetType().GetProperty("Updates");
                var updatesObj = updatesProp?.GetValue(updateInfo) as System.Collections.IEnumerable;
                if (updatesObj != null)
                {
                    object? firstItem = null;
                    foreach (var item in updatesObj)
                    {
                        if (firstItem == null)
                            firstItem = item;

                        // Try to match current version for build time
                        try
                        {
                            var verProp = item.GetType().GetProperty("Version");
                            var verVal = verProp?.GetValue(item)?.ToString();
                            if (!string.IsNullOrWhiteSpace(verVal))
                            {
                                // Exact match or prefix match (handles metadata like +commit)
                                if (string.Equals(verVal, currentVersion, StringComparison.OrdinalIgnoreCase) ||
                                    (verVal!.StartsWith(currentVersion, StringComparison.OrdinalIgnoreCase)))
                                {
                                    var pubDate = TryGetPublicationDate(item);
                                    if (pubDate != null)
                                        summary.CurrentPublishedOn = pubDate;
                                }
                            }
                        }
                        catch { /* ignore */ }
                    }

                    if (firstItem != null)
                    {
                        // Latest item details
                        try
                        {
                            var verProp = firstItem.GetType().GetProperty("Version");
                            summary.LatestVersion = verProp?.GetValue(firstItem)?.ToString();
                        }
                        catch { }

                        try
                        {
                            summary.LatestPublishedOn = TryGetPublicationDate(firstItem);
                        }
                        catch { }

                        try
                        {
                            var rnProp = firstItem.GetType().GetProperty("ReleaseNotesLink");
                            summary.LatestReleaseNotesLink = rnProp?.GetValue(firstItem)?.ToString();
                        }
                        catch { }
                    }
                }

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting app cast summary");
                return null;
            }
        }

        private static DateTimeOffset? TryGetPublicationDate(object item)
        {
            try
            {
                var t = item.GetType();
                // Try common property names
                var pubProp = t.GetProperty("PublicationDate") ?? t.GetProperty("PubDate") ?? t.GetProperty("PublishDate");
                var val = pubProp?.GetValue(item);
                if (val is DateTimeOffset dto)
                    return dto;
                if (val is DateTime dt)
                    return new DateTimeOffset(dt);
                if (val is string s && DateTimeOffset.TryParse(s, out var parsed))
                    return parsed;
            }
            catch { }
            return null;
        }

        public void Initialize(Window? mainWindow = null)
        {
            try
            {
                _logger.LogInformation("Initializing UpdateService");
                
                // NetSparkle doesn't support GitHub Atom feeds directly
                // We need to use a proper appcast format or GitHub releases integration
                // For now, using a generated appcast URL that points to a NetSparkle-compatible format
                // Alternative: Create appcast.xml in your repo or use a service like sparkle-project.org
                _appCastUrl = "https://raw.githubusercontent.com/DaoStudioAI/DaoStudio/main/appcast.xml";
                
                _sparkleUpdater = new SparkleUpdater(
                    _appCastUrl, // App cast URL
                    new Ed25519Checker(SecurityMode.Unsafe) // In production, use proper Ed25519 signatures
                )
                {
                    UIFactory = mainWindow?.Icon != null ? new UIFactory(mainWindow.Icon) : new UIFactory(),
                    RelaunchAfterUpdate = true, // Set to true to restart app after update
                    CustomInstallerArguments = ""
                };

                // Subscribe to events for logging
                _sparkleUpdater.UpdateDetected += OnUpdateDetected;
                
                _logger.LogInformation("UpdateService initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize UpdateService");
            }
        }

        public async Task StartUpdateCheckAsync(bool showUI = false)
        {
            if (_sparkleUpdater == null)
            {
                _logger.LogWarning("UpdateService not initialized. Call Initialize() first.");
                return;
            }

            try
            {
                _logger.LogInformation("Starting update check");
                
                if (showUI)
                {
                    // Show UI while checking for updates
                    await _sparkleUpdater.CheckForUpdatesAtUserRequest();
                }
                else
                {
                    // Check quietly in the background
                    var updateInfo = await _sparkleUpdater.CheckForUpdatesQuietly();
                    if (updateInfo != null && updateInfo.Updates?.Count > 0)
                    {
                        _logger.LogInformation($"Update available: {updateInfo.Updates[0].Version}");
                    }
                    else
                    {
                        _logger.LogInformation("No updates available");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update check");
            }
        }

        public void StartAutomaticUpdateCheck()
        {
            if (_sparkleUpdater == null)
            {
                _logger.LogWarning("UpdateService not initialized. Call Initialize() first.");
                return;
            }

            try
            {
                _logger.LogInformation("Starting automatic update check loop");
                // Start the automatic update check loop
                _sparkleUpdater.StartLoop(true); // true = check immediately on start
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting automatic update check");
            }
        }

        public void CheckForUpdatesManually()
        {
            if (_sparkleUpdater == null)
            {
                _logger.LogWarning("UpdateService not initialized. Call Initialize() first.");
                return;
            }

            try
            {
                _logger.LogInformation("Manual update check requested by user");
                _sparkleUpdater.CheckForUpdatesAtUserRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual update check");
            }
        }

        private void OnUpdateDetected(object? sender, UpdateDetectedEventArgs e)
        {
            _logger.LogInformation("Update detected");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    _logger.LogInformation("Disposing UpdateService");
                    
                    if (_sparkleUpdater != null)
                    {
                        _sparkleUpdater.UpdateDetected -= OnUpdateDetected;
                        _sparkleUpdater.Dispose();
                        _sparkleUpdater = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing UpdateService");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
