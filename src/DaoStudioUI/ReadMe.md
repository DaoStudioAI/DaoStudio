This is a C# Avalonia project—a ChatGPT UI client that supports multiple LLMs. It is somewhat similar to SillyTavern.

## Architecture
This application follows a three-tier architecture.
It is professionally designed, architected, and implemented.
The current folder contains the UI component, while the adjacent DBStorage folder handles storage.

## UI Design Principles
This application follows professional, modern UI design principles, including but not limit by:
- Clean, minimalist UI with consistent spacing and alignment
- Responsive layouts that work well on different screen sizes
- Proper use of typography hierarchy and whitespace
- Consistent color scheme that supports both light and dark themes
- Intuitive user workflows with clear feedback mechanisms

## Logging
This application uses Serilog for structured logging.

Environment variable: `DAOSTUDIO_LOG_LEVEL`
- Purpose: control the application log level at startup.
- Format: use Microsoft `LogLevel` names (Trace, Debug, Information, Warning, Error, Critical, None).
- Example (PowerShell): `$env:DAOSTUDIO_LOG_LEVEL = "Debug"`

## Internationalization and Resources
The application uses .NET's resource system for string localization. All user-visible strings should be defined in the `Resources/Strings.resx` file and accessed through the `Strings` class. This approach ensures:

When adding new features, avoid hardcoding strings directly in the code. Instead:
1. Add new entries to the resource file
2. Reference them using `Strings.ResourceName` in your code
