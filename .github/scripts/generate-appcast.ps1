#!/usr/bin/env pwsh
# Generate NetSparkle appcast.xml from GitHub releases

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$ReleaseUrl,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "appcast.xml"
)

$ErrorActionPreference = "Stop"

Write-Host "Generating appcast.xml for version $Version"

# Get current date in RFC822 format
$pubDate = (Get-Date).ToUniversalTime().ToString("R")

# Create the appcast XML content
$appcastContent = @"
<?xml version="1.0" encoding="utf-8"?>
<rss xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle" version="2.0">
  <channel>
    <title>DaoStudio</title>
    <link>https://github.com/DaoStudioAI/DaoStudio</link>
    <description>DaoStudio Updates</description>
    <language>en</language>
    
    <item>
      <title>DaoStudio v$Version</title>
      <link>$ReleaseUrl</link>
      <sparkle:version>$Version</sparkle:version>
      <description><![CDATA[
        <h2>DaoStudio v$Version</h2>
        <p>Download the appropriate package for your platform from the <a href="$ReleaseUrl">release page</a>.</p>
      ]]></description>
      <pubDate>$pubDate</pubDate>
      
      <!-- Windows x64 -->
      <enclosure 
        url="https://github.com/DaoStudioAI/DaoStudio/releases/download/v$Version/DaoStudio-Windows-x64-v$Version.zip"
        sparkle:version="$Version"
        sparkle:os="windows"
        type="application/zip" />
      
      <!-- Windows x86 -->
      <enclosure 
        url="https://github.com/DaoStudioAI/DaoStudio/releases/download/v$Version/DaoStudio-Windows-x86-v$Version.zip"
        sparkle:version="$Version"
        sparkle:os="windows"
        type="application/zip" />
      
      <!-- Windows ARM64 -->
      <enclosure 
        url="https://github.com/DaoStudioAI/DaoStudio/releases/download/v$Version/DaoStudio-Windows-ARM64-v$Version.zip"
        sparkle:version="$Version"
        sparkle:os="windows"
        type="application/zip" />
      
      <!-- Linux x64 -->
      <enclosure 
        url="https://github.com/DaoStudioAI/DaoStudio/releases/download/v$Version/DaoStudio-Linux-x64-v$Version.zip"
        sparkle:version="$Version"
        sparkle:os="linux"
        type="application/zip" />
      
      <!-- Linux ARM64 -->
      <enclosure 
        url="https://github.com/DaoStudioAI/DaoStudio/releases/download/v$Version/DaoStudio-Linux-ARM64-v$Version.zip"
        sparkle:version="$Version"
        sparkle:os="linux"
        type="application/zip" />
      
      <!-- macOS x64 -->
      <enclosure 
        url="https://github.com/DaoStudioAI/DaoStudio/releases/download/v$Version/DaoStudio-macOS-x64-v$Version.zip"
        sparkle:version="$Version"
        sparkle:os="macos"
        type="application/zip" />
      
      <!-- macOS ARM64 -->
      <enclosure 
        url="https://github.com/DaoStudioAI/DaoStudio/releases/download/v$Version/DaoStudio-macOS-ARM64-v$Version.zip"
        sparkle:version="$Version"
        sparkle:os="macos"
        type="application/zip" />
    </item>
    
  </channel>
</rss>
"@

# Write the appcast file
Set-Content -Path $OutputFile -Value $appcastContent -Encoding UTF8

Write-Host "Appcast file generated: $OutputFile"
