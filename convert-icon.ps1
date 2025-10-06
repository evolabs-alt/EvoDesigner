# Convert PNG to ICO for application icon
# This script converts icon.png to icon.ico with multiple sizes

param(
    [string]$InputFile = "icon.png",
    [string]$OutputFile = "icon.ico"
)

Write-Host "Converting $InputFile to $OutputFile..." -ForegroundColor Green

# Load System.Drawing assembly
Add-Type -AssemblyName System.Drawing

try {
    # Load the PNG image
    $pngImage = [System.Drawing.Image]::FromFile((Resolve-Path $InputFile))
    
    # Create a new icon with multiple sizes (16, 32, 48, 128)
    $sizes = @(16, 32, 48, 128)
    $images = @()
    
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($pngImage, 0, 0, $size, $size)
        $graphics.Dispose()
        $images += $bitmap
    }
    
    # Save as ICO
    $memoryStream = New-Object System.IO.MemoryStream
    
    # ICO header
    $writer = New-Object System.IO.BinaryWriter($memoryStream)
    $writer.Write([UInt16]0)  # Reserved
    $writer.Write([UInt16]1)  # Type (1 = ICO)
    $writer.Write([UInt16]$images.Count)  # Number of images
    
    $offset = 6 + ($images.Count * 16)  # Header + directory entries
    
    foreach ($image in $images) {
        $pngStream = New-Object System.IO.MemoryStream
        $image.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngStream.ToArray()
        
        # Write directory entry
        $width = if ($image.Width -eq 256) { 0 } else { [byte]$image.Width }
        $height = if ($image.Height -eq 256) { 0 } else { [byte]$image.Height }
        $writer.Write($width)                  # Width (0 = 256)
        $writer.Write($height)                 # Height (0 = 256)
        $writer.Write([byte]0)                 # Color palette
        $writer.Write([byte]0)                 # Reserved
        $writer.Write([UInt16]1)               # Color planes
        $writer.Write([UInt16]32)              # Bits per pixel
        $writer.Write([UInt32]$pngBytes.Length) # Size of image data
        $writer.Write([UInt32]$offset)         # Offset to image data
        
        $offset += $pngBytes.Length
        $pngStream.Dispose()
    }
    
    # Write image data
    foreach ($image in $images) {
        $pngStream = New-Object System.IO.MemoryStream
        $image.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngStream.ToArray()
        $writer.Write($pngBytes)
        $pngStream.Dispose()
        $image.Dispose()
    }
    
    $writer.Flush()
    $icoBytes = $memoryStream.ToArray()
    [System.IO.File]::WriteAllBytes((Join-Path $PWD $OutputFile), $icoBytes)
    
    $writer.Dispose()
    $memoryStream.Dispose()
    $pngImage.Dispose()
    
    Write-Host "Successfully created $OutputFile" -ForegroundColor Green
    Write-Host "Icon sizes included: $($sizes -join ', ') pixels" -ForegroundColor Cyan
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
