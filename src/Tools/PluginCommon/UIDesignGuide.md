# UI Design Guide

This guide documents the UI design principles, patterns, and standards used to ensure consistency and professional appearance across all UI components, including plugins.

## Overview

This app uses **Avalonia UI** with the **FluentAvalonia** theme library, following Microsoft's Fluent Design System principles. The application supports both light and dark themes with a modern, clean aesthetic.

## Core Design Principles

1. **Clean & Minimalist**: Focus on content with minimal visual clutter
2. **Responsive**: Layouts adapt to different window sizes
3. **Consistent**: Unified spacing, colors, and component usage
4. **Professional**: Enterprise-grade appearance with attention to detail
5. **Accessible**: Clear typography hierarchy and sufficient contrast
6. **Theme-aware**: Full support for light/dark theme switching

## Technology Stack

- **Framework**: Avalonia UI 11.3.1
- **Theme Library**: FluentAvalonia 2.3.0
- **Styling**: FluentAvaloniaTheme with user accent color preference
- **Icons**: Fluent UI Symbol Icons (SymbolIcon)

## Layout Standards

### Window Configuration
```xml
<Window TransparencyLevelHint="AcrylicBlur"
        Background="Transparent"
        WindowStartupLocation="CenterOwner">
```

### Spacing Guidelines
- **Component spacing**: 8, 16, or 24 pixels
- **Section margins**: 24 pixels for major sections
- **Card padding**: 16 pixels
- **Button spacing**: 8 pixels between buttons
- **List item spacing**: 16 pixels (StackPanel)

### Grid Layouts
- Use responsive column definitions: `*`, `Auto`
- Consistent gutter spacing: 8 pixels between columns
- Three-column layouts for complex dialogs (see AddModelView)

## Component Usage

### Cards/Panels
```xml
<Border Background="{StaticResource CardBackgroundFillColorDefaultBrush}" 
        CornerRadius="8" 
        Padding="16">
    <!-- Content -->
</Border>
```

### Navigation
- Use `ui:NavigationView` for main navigation
- Footer items for settings and tools
- Icon + Text pattern for menu items

### Buttons
```xml
<!-- Primary action -->
<Button Classes="accent" Content="Save" Width="100"/>

<!-- Secondary action -->
<Button Content="Cancel" Width="100"/>

<!-- Icon button -->
<Button Width="42" Height="42">
    <ui:SymbolIcon Symbol="Send"/>
</Button>
```

### Form Controls
```xml
<!-- Text input with label -->
<StackPanel Spacing="4">
    <TextBlock Text="Label" Classes="BodyStrong"/>
    <TextBox Text="{Binding Value}" 
             Watermark="Placeholder text"/>
</StackPanel>
```

## Color System

### Theme-aware Colors
The application uses theme-aware brushes that automatically adapt:

```xml
<!-- Background colors -->
{StaticResource CardBackgroundFillColorDefaultBrush}
{StaticResource CardBackgroundFillColorSecondaryBrush}
{StaticResource SolidBackgroundFillColorBaseBrush}

<!-- Text colors -->
{StaticResource TextFillColorPrimaryBrush}
{StaticResource TextFillColorSecondaryBrush}

<!-- Border/Stroke colors -->
{StaticResource CardStrokeColorDefaultBrush}
{StaticResource ControlStrokeColorDefaultBrush}

<!-- Interactive colors -->
{StaticResource ButtonBackgroundPressed}
{StaticResource SystemFillColorCriticalBrush}
```

## Typography

### Text Classes
- `Classes="TitleLarge"` - Section headers
- `Classes="BodyStrong"` - Field labels
- Default - Regular content
- `FontSize="12"` - Secondary/meta information
- `FontSize="10"` - Timestamps

### Font Families
- Default: System font
- Code: `FontFamily="Consolas, Monospace"`
- Icons: `FontFamily="{StaticResource SymbolThemeFontFamily}"`

## Icons

Use FluentAvalonia SymbolIcon for consistent iconography:
```xml
<ui:SymbolIcon Symbol="Home"/>
<ui:SymbolIcon Symbol="Settings"/>
<ui:SymbolIcon Symbol="People"/>
<ui:SymbolIcon Symbol="Send"/>
```

For custom icons, use TextBlock with symbol font:
```xml
<TextBlock Text="&#xE77B;" 
           FontFamily="{StaticResource SymbolThemeFontFamily}"
           FontSize="64"/>
```



## Localization

All user-visible strings must use resource references to support internationalization and maintain consistency across the application. Resources.Designer.cs is the generated file of Resources.resx. Don't try to modify *.Designer.cs files.


## Accessibility

1. **Keyboard Navigation**: Ensure all interactive elements are keyboard accessible
2. **Tooltips**: Provide helpful tooltips for icon-only buttons
3. **Contrast**: Use theme-aware colors that maintain WCAG compliance
4. **Focus Indicators**: Respect system focus indicators

## Plugin UI Guidelines

When creating plugin UI:

1. **Inherit Base Styles**: Use the same component patterns as the main application
2. **Theme Support**: Always use theme-aware brushes, never hardcode colors
3. **Consistent Spacing**: Follow the spacing guidelines (8, 16, 24px)
4. **Window Styling**: Match the transparent/acrylic style for dialogs
5. **Localization Ready**: Always use resource files for user-visible strings

### Plugin Config Window Template
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="using:FluentAvalonia.UI.Controls"
        xmlns:resx="clr-namespace:YourPlugin.Resources"
        Width="800" Height="600"
        WindowStartupLocation="CenterOwner"
        ShowInTaskbar="False"
        TransparencyLevelHint="AcrylicBlur"
        Background="Transparent"
        Title="{x:Static resx:Strings.Window_PluginConfiguration_Title}">
    
    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- Main content -->
        <Border Grid.Row="0" 
                Background="{StaticResource CardBackgroundFillColorDefaultBrush}"
                CornerRadius="8"
                Padding="16">
            <!-- Plugin-specific content -->
        </Border>
        
        <!-- Action buttons -->
        <StackPanel Grid.Row="1" 
                    Orientation="Horizontal"
                    HorizontalAlignment="Right"
                    Spacing="8"
                    Margin="0,16,0,0">
            <Button Content="{x:Static resx:Strings.Button_Save}" 
                    Classes="accent" Width="100"/>
            <Button Content="{x:Static resx:Strings.Button_Cancel}" 
                    Width="100"/>
        </StackPanel>
    </Grid>
</Window>
```

## Best Practices

1. **Consistency First**: When in doubt, copy patterns from existing UI
2. **Test Both Themes**: Always verify UI looks good in both light and dark themes
3. **Responsive Design**: Test with different window sizes
4. **Performance**: Use virtualization for large lists
5. **Error States**: Provide clear visual feedback for errors
6. **Loading States**: Show progress for long operations
7. **Empty States**: Design meaningful empty states with guidance
8. **Localization Ready**: Always use resource files for user-visible strings
