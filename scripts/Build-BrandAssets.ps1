param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../UWPHook/Resources'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
function Draw-Mark([int]$size) {
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size/128.0,$size/128.0)
    $tile = [Drawing.Drawing2D.GraphicsPath]::new()
    foreach($arc in @(@(3,3,60,60,180),@(65,3,60,60,270),@(65,65,60,60,0),@(3,65,60,60,90))) {
        $tile.AddArc($arc[0],$arc[1],$arc[2],$arc[3],$arc[4],90)
    }
    $tile.CloseFigure()
    $background = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#131D29'))
    $border = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#304856'),2)
    $g.FillPath($background,$tile); $g.DrawPath($border,$tile)
    $route = [Drawing.Drawing2D.GraphicsPath]::new()
    $route.AddLine(88,34,51,34); $route.AddBezier(51,34,27,34,27,70,51,70)
    $route.AddLine(51,70,77,70); $route.AddBezier(77,70,93,70,93,94,77,94); $route.AddLine(77,94,39,94)
    $gradient = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.Point]::new(20,20),[Drawing.Point]::new(100,110),[Drawing.ColorTranslator]::FromHtml('#B7EE85'),[Drawing.ColorTranslator]::FromHtml('#70C7FA'))
    $pen = [Drawing.Pen]::new($gradient,14)
    $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen,$route)
    $light = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#E5FFD0'))
    $blue = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#C4EDFF'))
    $g.FillEllipse($light,83,29,10,10); $g.FillEllipse($blue,34,89,10,10)
    $g.Dispose(); $tile.Dispose(); $background.Dispose(); $border.Dispose(); $route.Dispose(); $pen.Dispose(); $gradient.Dispose(); $light.Dispose(); $blue.Dispose()
    return $bitmap
}
$images = @()
foreach($size in @(16,24,32,48,64,128,256)) {
    $bitmap = Draw-Mark $size
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $images += @{Size=$size;Bytes=$stream.ToArray()}
    if($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'sGs-logo.png'),$stream.ToArray()) }
    $bitmap.Dispose(); $stream.Dispose()
}
$file = [IO.File]::Create((Join-Path $OutputDirectory 'sGs-app.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16*$images.Count
    foreach($item in $images) {
        $dimension = if($item.Size -eq 256){0}else{$item.Size}
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$item.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $item.Bytes.Length
    }
    foreach($item in $images) { $writer.Write([byte[]]$item.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output 'sGs-app vector mark, PNG and seven-size Windows icon prepared.'
