using DaoStudio.Common.Plugins;
using Microsoft.Win32;
using System.Diagnostics;
using Serilog;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Interfaces;

namespace BrowserTool;

internal class BrowserInfo
{
    public string Path { get; set; } = string.Empty;
    public BrowserType Type { get; set; } = BrowserType.Chrome;
}

public partial class BrowserToolPluginFactory : IPluginFactory
{
    private IHost? _host; // Stores the IHost instance
    
    /// <summary>
    /// Gets the current IHost instance
    /// </summary>
    public IHost? Host => _host;
    public async Task SetHost(IHost host)
    {
        _host = host;
        // Optional: Log host assignment for debugging
        // Console.WriteLine($"BrowserToolPlugin: Host set. Type: {host?.GetType().FullName}");
        await Task.CompletedTask;
    }    internal BrowserInfo GetDefaultBrowserPath()
    {
        try
        {

#if WINDOWS
                return GetWindowsDefaultBrowserPath();

#elif MACOS
                return GetMacOSDefaultBrowserPath();
#elif LINUX
            return GetLinuxDefaultBrowserPath();
#else
            throw new Exception("Unknow platform");
#endif
        }
        catch (Exception ex)
        {
            // Log exception if needed
            Log.Error(ex, "Error getting default browser path");
        }

        // Ultimate fallback - try to find any browser on the system
        return FindAnyBrowserOnSystem();
    }

#if WINDOWS
    private BrowserInfo GetWindowsDefaultBrowserPath()
    {
        string browserPath = string.Empty;
        BrowserType browserType = BrowserType.Unknown;
        
        // Method 1: Get from UserChoice registry
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice", false))
            {
                if (key != null)
                {
                    var progId = key.GetValue("ProgId") as string;
                    if (!string.IsNullOrEmpty(progId))
                    {
                        using (var browserKey = Registry.ClassesRoot.OpenSubKey($"{progId}\\shell\\open\\command", false))
                        {
                            if (browserKey != null)
                            {
                                var command = browserKey.GetValue(null) as string;
                                if (!string.IsNullOrEmpty(command))
                                {
                                    // Command might contain additional parameters, extract the path
                                    var parts = command.Split('"');
                                    if (parts.Length > 1)
                                    {
                                        browserPath = parts[1]; // The path is usually in the first pair of quotes
                                    }
                                    else
                                    {
                                        var executablePath = command.Split(' ')[0];
                                        browserPath = executablePath;
                                    }
                                    
                                    if (File.Exists(browserPath))
                                    {
                                        // Detect browser type from the executable path
                                        browserType = DetectBrowserTypeFromPath(browserPath);
                                        return new BrowserInfo { Path = browserPath, Type = browserType };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in UserChoice registry lookup");
        }
        
        // Method 2: Check default browser from StartMenuInternet key
        try
        {
            RegistryKey? startMenuKey = null;
            
            // Try HKCU first
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Clients\StartMenuInternet", false))
            {
                if (key != null)
                {
                    startMenuKey = key;
                }
            }
            
            // If not found in HKCU, try HKLM
            if (startMenuKey == null)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Clients\StartMenuInternet", false))
                {
                    if (key != null)
                    {
                        startMenuKey = key;
                    }
                }
            }
            
            if (startMenuKey != null)
            {
                var defaultBrowser = startMenuKey.GetValue(null) as string;
                if (string.IsNullOrEmpty(defaultBrowser))
                {
                    // If no default is set, just use the first entry
                    defaultBrowser = startMenuKey.GetSubKeyNames().FirstOrDefault();
                }
                
                if (!string.IsNullOrEmpty(defaultBrowser))
                {
                    using (var browserKey = startMenuKey.OpenSubKey(defaultBrowser + @"\shell\open\command", false))
                    {
                        if (browserKey != null)
                        {
                            var command = browserKey.GetValue(null) as string;
                            if (!string.IsNullOrEmpty(command))
                            {
                                var parts = command.Split('"');
                                if (parts.Length > 1)
                                {
                                    browserPath = parts[1];
                                }
                                else
                                {
                                    browserPath = command.Split(' ')[0];
                                }
                                
                                if (File.Exists(browserPath))
                                {
                                    // Detect browser type from the path and browser name
                                    browserType = DetectBrowserTypeFromPath(browserPath);
                                    if (browserType == BrowserType.Unknown && !string.IsNullOrEmpty(defaultBrowser))
                                    {
                                        // Try to detect from the registry key name if the path detection failed
                                        string lowerBrowserName = defaultBrowser.ToLowerInvariant();
                                        if (lowerBrowserName.Contains("chrome"))
                                            browserType = BrowserType.Chrome;
                                        else if (lowerBrowserName.Contains("firefox"))
                                            browserType = BrowserType.Firefox;
                                        else if (lowerBrowserName.Contains("edge"))
                                            browserType = BrowserType.Edge;
                                        else if (lowerBrowserName.Contains("safari"))
                                            browserType = BrowserType.Safari;
                                        else if (lowerBrowserName.Contains("opera"))
                                            browserType = BrowserType.Opera;
                                        else if (lowerBrowserName.Contains("brave"))
                                            browserType = BrowserType.Brave;
                                    }
                                    
                                    return new BrowserInfo { Path = browserPath, Type = browserType };
                                }
                            }
                        }
                    }
                }
                
                // Close the registry key when we're done
                startMenuKey.Close();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in StartMenuInternet registry lookup");
        }
        
        // Method 3: Try to find common browsers using environment variables
        try {
            // Use environment variables for Program Files paths
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            var commonWindowsBrowsers = new Dictionary<string, string[]>
            {
                {"Chrome", new[] {
                    Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                    Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe")
                }},
                {"Edge", new[] {
                    Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
                    Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe")
                }},
                {"Firefox", new[] {
                    Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"),
                    Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe")
                }},
                {"Opera", new[] {
                    Path.Combine(programFiles, "Opera", "launcher.exe"),
                    Path.Combine(programFilesX86, "Opera", "launcher.exe")
                }},
                {"Brave", new[] {
                    Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"),
                    Path.Combine(programFilesX86, "BraveSoftware", "Brave-Browser", "Application", "brave.exe")
                }}
            };
            
            foreach (var browser in commonWindowsBrowsers)
            {
                foreach (var path in browser.Value)
                {
                    if (File.Exists(path))
                    {
                        // Map the browser name to BrowserType enum
                        BrowserType type = BrowserType.Unknown;
                        if (Enum.TryParse(browser.Key, true, out BrowserType parsedType))
                        {
                            type = parsedType;
                        }
                        
                        return new BrowserInfo { Path = path, Type = type };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking common browsers");
        }
        
        // Method 4: Check Program Files for any browser EXEs
        try
        {
            string[] programDirs = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            
            foreach (var dir in programDirs)
            {
                if (Directory.Exists(dir))
                {
                    var browserExes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                        .Where(f => Path.GetFileName(f).ToLowerInvariant() is "chrome.exe" or "firefox.exe" or "msedge.exe" or "opera.exe" or "brave.exe" or "safari.exe" or "iexplore.exe")
                        .ToList();
                    
                    if (browserExes.Count > 0)
                    {
                        browserPath = browserExes[0];
                        browserType = DetectBrowserTypeFromPath(browserPath);
                        return new BrowserInfo { Path = browserPath, Type = browserType };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching Program Files");
        }
        
        return new BrowserInfo { Path = string.Empty, Type = BrowserType.Unknown };
    }
#endif
    

    private BrowserType DetectBrowserTypeFromPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return BrowserType.Unknown;

        string filename = Path.GetFileName(path).ToLowerInvariant();

        if (filename.Contains("chrome"))
            return BrowserType.Chrome;
        else if (filename.Contains("firefox"))
            return BrowserType.Firefox;
        else if (filename.Contains("msedge"))
            return BrowserType.Edge;
        else if (filename.Contains("safari"))
            return BrowserType.Safari;
        else if (filename.Contains("opera"))
            return BrowserType.Opera;
        else if (filename.Contains("brave"))
            return BrowserType.Brave;

        return BrowserType.Unknown;
    }

#if MACOS
    private BrowserInfo GetMacOSDefaultBrowserPath()
    {
        // Get user's home directory
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string applicationsDir = "/Applications";
        string userApplicationsDir = Path.Combine(homeDir, "Applications");
        string browserPath = string.Empty;
        BrowserType browserType = BrowserType.Unknown;
        
        // Method 1: Use LaunchServices to get the default browser
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/defaults",
                Arguments = "read com.apple.LaunchServices/com.apple.launchservices.secure LSHandlers",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (output.Contains("LSHandlerURLScheme = http") || output.Contains("LSHandlerURLScheme = https"))
                    {
                        var lines = output.Split('\n');
                        string bundleId = string.Empty;
                        
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Contains("LSHandlerURLScheme = http") || lines[i].Contains("LSHandlerURLScheme = https"))
                            {
                                // Look for the LSHandlerRoleAll line within the next few lines
                                for (int j = i; j < Math.Min(i + 10, lines.Length); j++)
                                {
                                    if (lines[j].Contains("LSHandlerRoleAll"))
                                    {
                                        var match = Regex.Match(lines[j], @"LSHandlerRoleAll\s*=\s*""?([^;""]+)""?");
                                        if (match.Success)
                                        {
                                            bundleId = match.Groups[1].Value.Trim();
                                            break;
                                        }
                                    }
                                }
                                if (!string.IsNullOrEmpty(bundleId)) break;
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(bundleId))
                        {
                            // Detect browser type from bundle ID
                            browserType = DetectBrowserTypeFromBundleId(bundleId);
                            
                            // Get the application path from the bundle ID
                            var appPathStartInfo = new ProcessStartInfo
                            {
                                FileName = "/usr/bin/mdfind",
                                Arguments = $"kMDItemCFBundleIdentifier = '{bundleId}'",
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (var appPathProcess = Process.Start(appPathStartInfo))
                            {
                                if (appPathProcess != null)
                                {
                                    var appPath = appPathProcess.StandardOutput.ReadToEnd().Trim();
                                    appPathProcess.WaitForExit();
                                    
                                    if (!string.IsNullOrEmpty(appPath) && Directory.Exists(appPath))
                                    {
                                        return new BrowserInfo { Path = appPath, Type = browserType };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in LaunchServices lookup");
        }
        
        // Method 2: Use 'open' command with -a flag to find Safari
        try
        {
            var checkSafariStartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                Arguments = "-a Safari --dry-run",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using (var process = Process.Start(checkSafariStartInfo))
            {
                if (process != null)
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        return new BrowserInfo { 
                            Path = Path.Combine(applicationsDir, "Safari.app"), 
                            Type = BrowserType.Safari 
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking Safari");
        }
        
        // Method 3: Check for common browsers in Applications folders (system and user)
        try {
            // Common browser names and their types
            var browserMappings = new Dictionary<string, BrowserType> {
                {"Google Chrome.app", BrowserType.Chrome},
                {"Firefox.app", BrowserType.Firefox},
                {"Safari.app", BrowserType.Safari},
                {"Microsoft Edge.app", BrowserType.Edge},
                {"Opera.app", BrowserType.Opera},
                {"Brave Browser.app", BrowserType.Brave}
            };
            
            // Check system Applications directory
            foreach (var browserMapping in browserMappings)
            {
                string path = Path.Combine(applicationsDir, browserMapping.Key);
                if (Directory.Exists(path))
                {
                    return new BrowserInfo { Path = path, Type = browserMapping.Value };
                }
            }
            
            // Check user Applications directory
            foreach (var browserMapping in browserMappings)
            {
                string path = Path.Combine(userApplicationsDir, browserMapping.Key);
                if (Directory.Exists(path))
                {
                    return new BrowserInfo { Path = path, Type = browserMapping.Value };
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking Applications directories");
        }
        
        // Method 4: Search all .app packages that might be browsers
        try
        {
            // Search both system and user Applications directories
            foreach (var appDir in new[] { applicationsDir, userApplicationsDir })
            {
                if (!Directory.Exists(appDir)) continue;
                
                var findBrowsersStartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/bin/find",
                    Arguments = $"{appDir} -name \"*.app\" -maxdepth 2 -type d | grep -i -E 'chrome|firefox|safari|edge|opera|brave'",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(findBrowsersStartInfo))
                {
                    if (process != null)
                    {
                        var foundBrowsers = process.StandardOutput.ReadToEnd().Trim().Split('\n');
                        process.WaitForExit();
                        
                        if (foundBrowsers.Length > 0 && !string.IsNullOrEmpty(foundBrowsers[0]))
                        {
                            browserPath = foundBrowsers[0];
                            browserType = DetectBrowserTypeFromPath(browserPath);
                            return new BrowserInfo { Path = browserPath, Type = browserType };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error finding browser apps");
        }
        
        // Ultimate fallback for macOS
        return new BrowserInfo { 
            Path = Path.Combine(applicationsDir, "Safari.app"), 
            Type = BrowserType.Safari 
        };
    }

#endif
    private BrowserType DetectBrowserTypeFromBundleId(string bundleId)
    {
        if (string.IsNullOrEmpty(bundleId))
            return BrowserType.Unknown;
            
        bundleId = bundleId.ToLowerInvariant();
        
        if (bundleId.Contains("chrome"))
            return BrowserType.Chrome;
        else if (bundleId.Contains("firefox"))
            return BrowserType.Firefox;
        else if (bundleId.Contains("safari"))
            return BrowserType.Safari;
        else if (bundleId.Contains("edge"))
            return BrowserType.Edge;
        else if (bundleId.Contains("opera"))
            return BrowserType.Opera;
        else if (bundleId.Contains("brave"))
            return BrowserType.Brave;
            
        return BrowserType.Unknown;
    }

#if LINUX
    private BrowserInfo GetLinuxDefaultBrowserPath()
    {
        // Get user's home directory
        string homeDir = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string browserPath = string.Empty;
        BrowserType browserType = BrowserType.Chrome;
        
        // Method 1: Use xdg-settings to get default browser
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xdg-settings",
                Arguments = "get default-web-browser",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                if (process != null)
                {
                    var browserDesktopFile = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(browserDesktopFile))
                    {
                        // Detect browser type from desktop file name
                        browserType = DetectBrowserTypeFromDesktopFile(browserDesktopFile);
                        
                        // Check in both user and system desktop file locations
                        string[] desktopPaths = {
                            Path.Combine("/usr/share/applications", browserDesktopFile),
                            Path.Combine("/usr/local/share/applications", browserDesktopFile),
                            Path.Combine(homeDir, ".local/share/applications", browserDesktopFile)
                        };
                        
                        foreach (var desktopPath in desktopPaths)
                        {
                            if (File.Exists(desktopPath))
                            {
                                // Extract Exec line from .desktop file
                                var execLine = File.ReadAllLines(desktopPath)
                                    .FirstOrDefault(line => line.StartsWith("Exec="));
                                
                                if (!string.IsNullOrEmpty(execLine))
                                {
                                    // Parse the Exec line to get the executable path
                                    var execPath = Regex.Match(execLine, @"Exec=([^ %""]+)");
                                    if (execPath.Success)
                                    {
                                        var path = execPath.Groups[1].Value;
                                        if (File.Exists(path))
                                        {
                                            return new BrowserInfo { Path = path, Type = browserType };
                                        }
                                        
                                        // If it's just a command without path, try to find it using 'which'
                                        if (!path.Contains('/'))
                                        {
                                            var whichStartInfo = new ProcessStartInfo
                                            {
                                                FileName = "which",
                                                Arguments = path,
                                                RedirectStandardOutput = true,
                                                UseShellExecute = false,
                                                CreateNoWindow = true
                                            };
                                            
                                            using (var whichProcess = Process.Start(whichStartInfo))
                                            {
                                                if (whichProcess != null)
                                                {
                                                    var fullPath = whichProcess.StandardOutput.ReadToEnd().Trim();
                                                    whichProcess.WaitForExit();
                                                    
                                                    if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                                                    {
                                                        return new BrowserInfo { Path = fullPath, Type = browserType };
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Linux default browser with xdg-settings");
        }
        
        // Method 2: Try alternative approach using xdg-mime
        try
        {
            var mimeStartInfo = new ProcessStartInfo
            {
                FileName = "xdg-mime",
                Arguments = "query default x-scheme-handler/http",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using (var process = Process.Start(mimeStartInfo))
            {
                if (process != null)
                {
                    var desktopFile = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    
                    if (!string.IsNullOrEmpty(desktopFile))
                    {
                        // Detect browser type from desktop file
                        browserType = DetectBrowserTypeFromDesktopFile(desktopFile);
                        
                        // Look for the desktop file in standard locations
                        string[] desktopPaths = {
                            Path.Combine("/usr/share/applications", desktopFile),
                            Path.Combine("/usr/local/share/applications", desktopFile),
                            Path.Combine(homeDir, ".local/share/applications", desktopFile)
                        };
                        
                        foreach (var path in desktopPaths)
                        {
                            if (File.Exists(path))
                            {
                                // Parse the desktop file to get the Exec line
                                foreach (var line in File.ReadAllLines(path))
                                {
                                    if (line.StartsWith("Exec="))
                                    {
                                        var parts = line.Substring(5).Split(' ');
                                        var execPath = parts[0].Replace("%u", "").Replace("%U", "");
                                        
                                        // If it's a full path
                                        if (File.Exists(execPath))
                                        {
                                            return new BrowserInfo { Path = execPath, Type = browserType };
                                        }
                                        
                                        // Otherwise try to find it with 'which'
                                        var whichStartInfo = new ProcessStartInfo
                                        {
                                            FileName = "which",
                                            Arguments = execPath,
                                            RedirectStandardOutput = true,
                                            UseShellExecute = false,
                                            CreateNoWindow = true
                                        };
                                        
                                        using (var whichProcess = Process.Start(whichStartInfo))
                                        {
                                            if (whichProcess != null)
                                            {
                                                var fullPath = whichProcess.StandardOutput.ReadToEnd().Trim();
                                                whichProcess.WaitForExit();
                                                
                                                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                                                {
                                                    return new BrowserInfo { Path = fullPath, Type = browserType };
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting Linux default browser with xdg-mime");
        }
        
        // Method 3: Check common browser locations using environment variables
        try {
            // Common paths where browsers might be installed
            string[] commonPaths = {
                "/usr/bin",
                "/usr/local/bin",
                "/snap/bin",
                "/opt/google/chrome",
                "/opt/mozilla/bin"
            };
            
            // Common browser executable names with their types
            var browserExes = new Dictionary<string, BrowserType> {
                {"firefox", BrowserType.Firefox},
                {"google-chrome", BrowserType.Chrome},
                {"google-chrome-stable", BrowserType.Chrome},
                {"chromium", BrowserType.Chrome},
                {"chromium-browser", BrowserType.Chrome},
                {"brave-browser", BrowserType.Brave},
                {"opera", BrowserType.Opera},
                {"microsoft-edge", BrowserType.Edge},
                {"epiphany-browser", BrowserType.Unknown},
                {"konqueror", BrowserType.Unknown},
                {"falkon", BrowserType.Unknown},
                {"midori", BrowserType.Unknown}
            };
            
            // Check each potential path
            foreach (var browserExe in browserExes)
            {
                foreach (var path in commonPaths)
                {
                    string fullPath = Path.Combine(path, browserExe.Key);
                    if (File.Exists(fullPath))
                    {
                        return new BrowserInfo { Path = fullPath, Type = browserExe.Value };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking common browser locations");
        }
        
        // Method 4: Use 'locate' command to find browser executables
        try
        {
            var browserNames = new Dictionary<string, BrowserType> {
                {"firefox", BrowserType.Firefox},
                {"chrome", BrowserType.Chrome},
                {"chromium", BrowserType.Chrome},
                {"brave", BrowserType.Brave},
                {"opera", BrowserType.Opera},
                {"edge", BrowserType.Edge}
            };
            
            foreach (var browser in browserNames)
            {
                var locateStartInfo = new ProcessStartInfo
                {
                    FileName = "locate",
                    Arguments = $"-l 1 bin/{browser.Key}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(locateStartInfo))
                {
                    if (process != null)
                    {
                        var path = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();
                        
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            return new BrowserInfo { Path = path, Type = browser.Value };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error using locate to find browsers");
        }
        
        return new BrowserInfo { Path = string.Empty, Type = BrowserType.Unknown};
    }
#endif

    private BrowserType DetectBrowserTypeFromDesktopFile(string desktopFile)
    {
        if (string.IsNullOrEmpty(desktopFile))
            return BrowserType.Unknown;
            
        string lowerFile = desktopFile.ToLowerInvariant();
        
        if (lowerFile.Contains("chrome"))
            return BrowserType.Chrome;
        else if (lowerFile.Contains("firefox"))
            return BrowserType.Firefox;
        else if (lowerFile.Contains("edge"))
            return BrowserType.Edge;
        else if (lowerFile.Contains("opera"))
            return BrowserType.Opera;
        else if (lowerFile.Contains("brave"))
            return BrowserType.Brave;
            
        return BrowserType.Unknown;
    }

    // Ultimate fallback - search the entire system for common browser executables
    private BrowserInfo FindAnyBrowserOnSystem()
    {
#if WINDOWS 
        try
        {
            // Search in common Windows locations for browser executables
            var commonExecutables = new Dictionary<string, BrowserType> {
                {"chrome.exe", BrowserType.Chrome}, 
                {"firefox.exe", BrowserType.Firefox}, 
                {"msedge.exe", BrowserType.Edge}, 
                {"iexplore.exe", BrowserType.Unknown}, 
                {"opera.exe", BrowserType.Opera}, 
                {"brave.exe", BrowserType.Brave}
            };
            
            // Use environment variables instead of hardcoded paths
            var searchDirs = new[] 
            { 
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
            };
            
            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;
                
                // Try to find any browser executable recursively
                foreach (var executable in commonExecutables)
                {
                    try
                    {
                        var files = Directory.GetFiles(dir, executable.Key, SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            return new BrowserInfo { Path = files[0], Type = executable.Value };
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error searching for {Executable}", executable);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in ultimate fallback search");
        }
            return new BrowserInfo { Path = string.Empty, Type = BrowserType.Unknown };
#elif OSX || MACOS || MACCATALYST
        // Use environment variables for home directory
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string userApplicationsDir = Path.Combine(homeDir, "Applications");
        
        // Check both system and user Applications directories
        foreach (var appDir in new[] { "/Applications", userApplicationsDir })
        {
            if (Directory.Exists(appDir))
            {
                // Check for Safari first as it's always installed on macOS
                string safariPath = Path.Combine(appDir, "Safari.app");
                if (Directory.Exists(safariPath))
                {
                    return new BrowserInfo { Path = safariPath, Type = BrowserType.Safari };
                }
                
                // Then check for any other browser
                var browsers = new Dictionary<string, BrowserType> {
                    {"Google Chrome.app", BrowserType.Chrome}, 
                    {"Firefox.app", BrowserType.Firefox}, 
                    {"Microsoft Edge.app", BrowserType.Edge}, 
                    {"Opera.app", BrowserType.Opera}, 
                    {"Brave Browser.app", BrowserType.Brave}
                };
                
                foreach (var browser in browsers)
                {
                    string path = Path.Combine(appDir, browser.Key);
                    if (Directory.Exists(path))
                    {
                        return new BrowserInfo { Path = path, Type = browser.Value };
                    }
                }
            }
        }
        
        // Last resort fallback
        return new BrowserInfo { Path = "/Applications/Safari.app", Type = BrowserType.Safari };
#elif LINUX 
        // Last resort for Linux - check if any browser is installed with 'which'
        string homeDir = Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        try
        {
            var commonBrowserCommands = new Dictionary<string, BrowserType> {
                {"firefox", BrowserType.Firefox},
                {"google-chrome", BrowserType.Chrome},
                {"chromium-browser", BrowserType.Chrome},
                {"epiphany", BrowserType.Default},
                {"konqueror", BrowserType.Default},
                {"brave-browser", BrowserType.Brave}
            };
            
            foreach (var command in commonBrowserCommands)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command.Key,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        var path = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();
                        
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            return new BrowserInfo { Path = path, Type = command.Value };
                        }
                    }
                }
            }
            
            // Also check XDG_CONFIG_HOME and XDG_DATA_HOME for browser configs
            string xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? 
                                  Path.Combine(homeDir, ".config");
            string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? 
                               Path.Combine(homeDir, ".local/share");
            
            // Check for browser configurations in these directories
            foreach (var dir in new[] { xdgConfigHome, xdgDataHome })
            {
                if (Directory.Exists(dir))
                {
                    var browserDirs = Directory.GetDirectories(dir)
                        .Where(d => Path.GetFileName(d).ToLowerInvariant().Contains("firefox") || 
                               Path.GetFileName(d).ToLowerInvariant().Contains("chrome") || 
                               Path.GetFileName(d).ToLowerInvariant().Contains("brave"))
                        .ToList();
                    
                    if (browserDirs.Count > 0)
                    {
                        // Found browser config, now try to find the executable
                        var browserNames = new Dictionary<string, BrowserType> {
                            {"firefox", BrowserType.Firefox},
                            {"google-chrome", BrowserType.Chrome},
                            {"chromium", BrowserType.Chrome},
                            {"brave-browser", BrowserType.Brave}
                        };
                        
                        foreach (var browserName in browserNames)
                        {
                            var whichStartInfo = new ProcessStartInfo
                            {
                                FileName = "which",
                                Arguments = browserName.Key,
                                RedirectStandardOutput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            
                            using (var process = Process.Start(whichStartInfo))
                            {
                                if (process != null)
                                {
                                    var path = process.StandardOutput.ReadToEnd().Trim();
                                    process.WaitForExit();
                                    
                                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                    {
                                        return new BrowserInfo { Path = path, Type = browserName.Value };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in ultimate Linux fallback");
        }
#else
        return new BrowserInfo { Path = string.Empty, Type = BrowserType.Unknown };
#endif

    }
}
