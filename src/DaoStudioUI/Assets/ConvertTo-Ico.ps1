<#
ConvertTo-Ico.ps1
Converts an image (png/jpg/etc) to a multi-size .ico file containing multiple PNG-encoded icon images (sizes 16,32,48,64,128,256).
Usage:
  .\ConvertTo-Ico.ps1 -Input .\logo.png -Output .\logo.ico
If -Output is omitted, the script will place the .ico next to the input file with the same base name.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$InputPath,

    [Parameter(Mandatory=$false, Position=1)]
    [string]$OutputPath,

    [int[]]$Sizes = @(16,32,48,64,128,256)
)

function Write-IconFile {
    param(
        [Parameter(Mandatory=$true)] [string]$OutputPath,
        [Parameter(Mandatory=$true)] [System.Collections.Generic.List[byte[]]]$PngImages,
        [Parameter(Mandatory=$true)] [int[]]$Sizes
    )

    # ICO header: Reserved (2 bytes), Type (2 bytes), Count (2 bytes)
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    $writer.Write([System.UInt16]0)    # reserved
    $writer.Write([System.UInt16]1)    # type = 1 for icon
    $writer.Write([System.UInt16]$PngImages.Count)

    # Calculate directory entries
    $offset = 6 + (16 * $PngImages.Count) # header + dir entries
    for ($i = 0; $i -lt $PngImages.Count; $i++) {
        $imgBytes = $PngImages[$i]
        $size = $Sizes[$i]
        # width and height bytes: 0 for 256
        $widthByte = if ($size -ge 256) { 0 } else { [byte]$size }
        $heightByte = $widthByte
        $writer.Write([byte]$widthByte)
        $writer.Write([byte]$heightByte)
        $writer.Write([byte]0) # color palette
        $writer.Write([byte]0) # reserved
        $writer.Write([System.UInt16]0) # color planes
        $writer.Write([System.UInt16]32) # bits per pixel
        $writer.Write([System.UInt32]$imgBytes.Length)
        $writer.Write([System.UInt32]$offset)
        $offset += $imgBytes.Length
    }

    # Write image data
    for ($i = 0; $i -lt $PngImages.Count; $i++) {
        $writer.Write($PngImages[$i])
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())

    $writer.Close()
    $stream.Close()
}

function Get-PngFromBitmap([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    # Save as PNG to preserve alpha
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Close()
    return $bytes
}

# Validate input file
$inputPath = $null
# Fallbacks for different invocation styles. Check bound params, $args, and invocation line.
Write-Host "PSBoundParameters: $($PSBoundParameters.Keys -join ',')"
Write-Host "args count: $($args.Count)"

if (-not $InputPath) {
    if ($PSBoundParameters.ContainsKey('InputPath')) {
        $InputPath = $PSBoundParameters['InputPath']
    } elseif ($args.Count -ge 1) {
        $InputPath = $args[0]
    } else {
        # Try to parse quoted strings from the invocation line (best-effort when called via other runners)
        $line = $MyInvocation.Line
        if ($line) {
            $matches = [regex]::Matches($line, '"([^"]+)"')
            if ($matches.Count -ge 1) { $InputPath = $matches[0].Groups[1].Value }
            if (-not $OutputPath -and $matches.Count -ge 2) { $OutputPath = $matches[1].Groups[1].Value }
        }
    }
}

Write-Host "ConvertTo-Ico: InputPath='$InputPath'  OutputPath='$OutputPath'"

if (-not $InputPath) {
    Write-Error "No input path provided. Specify -InputPath <file> or pass the path as the first argument."
    exit 1
}

$inputPath = (Resolve-Path -Path $InputPath -ErrorAction Stop).ProviderPath

# Determine output path. Use GetFullPath so it works even if parent doesn't yet exist.
if (-not $OutputPath) {
    $OutputPath = [System.IO.Path]::ChangeExtension($inputPath, '.ico')
} else {
    try {
        $OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    } catch {
        # Fallback: combine with current directory
        $OutputPath = [System.IO.Path]::Combine((Get-Location).ProviderPath, $OutputPath)
    }
}

# Load System.Drawing (works with Windows PowerShell / .NET Framework). For PowerShell Core on Windows, System.Drawing.Common may be present but has platform limitations.
# Load System.Drawing (works on Windows PowerShell / .NET Framework). If it fails, surface a helpful error.
try {
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
} catch {
    Write-Error "Failed to load System.Drawing assembly. Ensure you're running on Windows with .NET Framework or have System.Drawing.Common available: $_"
    exit 1
}

try {
    $src = [System.Drawing.Image]::FromFile($inputPath)
} catch {
    Write-Error "Failed to load image from path '$inputPath': $_"
    exit 1
}

# Prepare PNG-encoded images list
$pngList = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $Sizes) {
    # Create square bitmap of requested size
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Calculate aspect-fit destination rect
    $srcWidth = $src.Width
    $srcHeight = $src.Height
    if ($srcWidth -eq 0 -or $srcHeight -eq 0) { continue }

    $ratio = [math]::Min($size / $srcWidth, $size / $srcHeight)
    $drawWidth = [int]([math]::Round($srcWidth * $ratio))
    $drawHeight = [int]([math]::Round($srcHeight * $ratio))
    $dx = [int](($size - $drawWidth) / 2)
    $dy = [int](($size - $drawHeight) / 2)

    $g.DrawImage($src, $dx, $dy, $drawWidth, $drawHeight)
    $g.Dispose()

    $png = Get-PngFromBitmap -bmp $bmp
    $pngList.Add($png)
    $bmp.Dispose()
}

# Write the .ico file
try {
    Write-Host "Writing $OutputPath..."
    Write-IconFile -OutputPath $OutputPath -PngImages $pngList -Sizes $Sizes
    Write-Host "Created: $OutputPath"
} catch {
    Write-Error "Failed to write ICO: $_"
    exit 1
} finally {
    $src.Dispose()
}

# EOF
