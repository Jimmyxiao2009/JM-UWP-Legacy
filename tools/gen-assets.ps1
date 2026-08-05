param([string]$dir)
Add-Type -AssemblyName System.Drawing
$bg = [System.Drawing.Color]::FromArgb(194, 24, 91)
$items = @{
  "StoreLogo.png"         = @(50, 50)
  "Square44x44Logo.png"   = @(44, 44)
  "Square71x71Logo.png"   = @(71, 71)
  "Square150x150Logo.png" = @(150, 150)
  "Square310x310Logo.png" = @(310, 310)
  "Wide310x150Logo.png"   = @(310, 150)
  "SplashScreen.png"      = @(620, 300)
  "LockScreenLogo.png"    = @(24, 24)
}
foreach ($k in $items.Keys) {
  $w = $items[$k][0]; $h = $items[$k][1]
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear($bg)
  if ($w -ge 71) {
    $fs = [int]($h * 0.42)
    $font = New-Object System.Drawing.Font("Segoe UI", $fs, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $w, $h)
    $g.DrawString("JM", $font, [System.Drawing.Brushes]::White, $rect, $sf)
    $font.Dispose()
  }
  $g.Dispose()
  $bmp.Save((Join-Path $dir $k), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  "$k  $($w)x$($h)"
}
