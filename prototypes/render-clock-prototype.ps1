Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$out = Join-Path $PSScriptRoot 'apple-clock-glass-variants.png'
$w = 1800
$h = 1180
$bmp = New-Object Drawing.Bitmap $w, $h
$g = [Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit

function RoundedPath([single]$x, [single]$y, [single]$width, [single]$height, [single]$radius) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $width - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $width - $d, $y + $height - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function DrawWallpaper($g, $w, $h) {
    $rect = New-Object Drawing.Rectangle 0, 0, $w, $h
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::FromArgb(255, 20, 36, 52)), ([Drawing.Color]::FromArgb(255, 57, 84, 96)), 18
    $g.FillRectangle($bg, $rect)
    $bg.Dispose()

    $bands = @(
        @{ x=-180; y=90;  w=980; h=260; a=-9;  c=[Drawing.Color]::FromArgb(210, 229, 91, 108) },
        @{ x=670;  y=-40; w=940; h=240; a=12;  c=[Drawing.Color]::FromArgb(210, 91, 181, 176) },
        @{ x=980;  y=450; w=1040;h=270; a=-14; c=[Drawing.Color]::FromArgb(205, 242, 176, 72) },
        @{ x=-120; y=690; w=1050;h=260; a=10;  c=[Drawing.Color]::FromArgb(195, 65, 96, 170) }
    )
    foreach ($b in $bands) {
        $state = $g.Save()
        $g.TranslateTransform($b.x + $b.w/2, $b.y + $b.h/2)
        $g.RotateTransform($b.a)
        $r = New-Object Drawing.RectangleF (-$b.w/2), (-$b.h/2), $b.w, $b.h
        $brush = New-Object Drawing.SolidBrush $b.c
        $g.FillRectangle($brush, $r)
        $brush.Dispose()
        $g.Restore($state)
    }

    $linePen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(48,255,255,255)), 2
    for ($i=0; $i -lt 14; $i++) {
        $y = 90 + $i * 73
        $g.DrawLine($linePen, 0, $y, $w, $y + 160)
    }
    $linePen.Dispose()
}

function DrawTicks($g, [single]$x, [single]$y, [single]$size, [Drawing.Color]$color) {
    $inset = $size * 0.095
    $left = $x + $inset
    $top = $y + $inset
    $width = $size - 2*$inset
    $height = $width
    $radius = $size * 0.125
    $straightX = $width - 2*$radius
    $straightY = $height - 2*$radius
    $arc = [Math]::PI * $radius / 2
    $perimeter = 2*$straightX + 2*$straightY + 4*$arc
    for ($i=0; $i -lt 60; $i++) {
        $s = $perimeter * $i / 60
        $px = 0.0; $py = 0.0; $nx = 0.0; $ny = 0.0
        if ($s -lt $straightX) {
            $px=$left+$radius+$s; $py=$top; $nx=0; $ny=1
        } elseif (($s-=$straightX) -lt $arc) {
            $a=-[Math]::PI/2+$s/$radius; $px=$left+$width-$radius+$radius*[Math]::Cos($a); $py=$top+$radius+$radius*[Math]::Sin($a); $nx=-[Math]::Cos($a); $ny=-[Math]::Sin($a)
        } elseif (($s-=$arc) -lt $straightY) {
            $px=$left+$width; $py=$top+$radius+$s; $nx=-1; $ny=0
        } elseif (($s-=$straightY) -lt $arc) {
            $a=$s/$radius; $px=$left+$width-$radius+$radius*[Math]::Cos($a); $py=$top+$height-$radius+$radius*[Math]::Sin($a); $nx=-[Math]::Cos($a); $ny=-[Math]::Sin($a)
        } elseif (($s-=$arc) -lt $straightX) {
            $px=$left+$width-$radius-$s; $py=$top+$height; $nx=0; $ny=-1
        } elseif (($s-=$straightX) -lt $arc) {
            $a=[Math]::PI/2+$s/$radius; $px=$left+$radius+$radius*[Math]::Cos($a); $py=$top+$height-$radius+$radius*[Math]::Sin($a); $nx=-[Math]::Cos($a); $ny=-[Math]::Sin($a)
        } elseif (($s-=$arc) -lt $straightY) {
            $px=$left; $py=$top+$height-$radius-$s; $nx=1; $ny=0
        } else {
            $s-=$straightY; $a=[Math]::PI+$s/$radius; $px=$left+$radius+$radius*[Math]::Cos($a); $py=$top+$radius+$radius*[Math]::Sin($a); $nx=-[Math]::Cos($a); $ny=-[Math]::Sin($a)
        }
        $major = ($i % 5 -eq 0)
        $length = if ($major) { $size*0.062 } else { $size*0.042 }
        $pen = New-Object Drawing.Pen $color, ($(if ($major) { 5.2 } else { 3.3 }))
        $pen.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($pen, [single]$px, [single]$py, [single]($px+$nx*$length), [single]($py+$ny*$length))
        $pen.Dispose()
    }
}

function DrawClock($g, [single]$x, [single]$y, [single]$size, [string]$material) {
    $radius = $size * 0.168
    $path = RoundedPath $x $y $size $size $radius

    $shadowPath = RoundedPath ($x+2) ($y+12) $size $size $radius
    $shadow = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(58,0,0,0))
    $g.FillPath($shadow, $shadowPath)
    $shadow.Dispose(); $shadowPath.Dispose()

    if ($material -eq 'reference') {
        $fill = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255,250,250,248))
        $g.FillPath($fill, $path)
        $fill.Dispose()
        $tick = [Drawing.Color]::FromArgb(230,24,25,24)
        $digit = [Drawing.Color]::FromArgb(255,3,3,3)
    } elseif ($material -eq 'liquid') {
        $rect = New-Object Drawing.RectangleF $x, $y, $size, $size
        $fill = New-Object Drawing.Drawing2D.LinearGradientBrush $rect, ([Drawing.Color]::FromArgb(174,255,255,255)), ([Drawing.Color]::FromArgb(105,205,235,238)), 115
        $g.FillPath($fill, $path)
        $fill.Dispose()
        $rim = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(190,255,255,255)), 3
        $g.DrawPath($rim, $path); $rim.Dispose()
        $hiPath = RoundedPath ($x+12) ($y+10) ($size-24) ($size*0.43) ($radius-8)
        $hi = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.RectangleF ($x+12),($y+10),($size-24),($size*0.43)), ([Drawing.Color]::FromArgb(125,255,255,255)), ([Drawing.Color]::FromArgb(0,255,255,255)), 90
        $g.FillPath($hi, $hiPath); $hi.Dispose(); $hiPath.Dispose()
        $tick = [Drawing.Color]::FromArgb(220,16,28,31)
        $digit = [Drawing.Color]::FromArgb(255,8,18,20)
    } else {
        $fill = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(208,238,243,242))
        $g.FillPath($fill, $path); $fill.Dispose()
        $rim = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(120,255,255,255)), 2
        $g.DrawPath($rim, $path); $rim.Dispose()
        $noise = New-Object System.Random 48
        $speck = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(18,255,255,255))
        for ($i=0; $i -lt 850; $i++) {
            $nx = $x + $noise.NextDouble()*$size
            $ny = $y + $noise.NextDouble()*$size
            if ($path.IsVisible($nx,$ny)) { $g.FillEllipse($speck,[single]$nx,[single]$ny,1.4,1.4) }
        }
        $speck.Dispose()
        $tick = [Drawing.Color]::FromArgb(205,27,38,40)
        $digit = [Drawing.Color]::FromArgb(245,13,24,26)
    }

    DrawTicks $g $x $y $size $tick
    $font = New-Object Drawing.Font 'Bahnschrift SemiCondensed', ($size*0.25), ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
    $textBrush = New-Object Drawing.SolidBrush $digit
    $textPath = New-Object Drawing.Drawing2D.GraphicsPath
    $textPath.FillMode = [Drawing.Drawing2D.FillMode]::Winding
    $textPath.AddString('15:48', $font.FontFamily, [int]$font.Style, $font.Size, (New-Object Drawing.PointF 0,0), [Drawing.StringFormat]::GenericTypographic)
    $inkBounds = $textPath.GetBounds()
    $move = New-Object Drawing.Drawing2D.Matrix
    $move.Translate(
        [single]($x + $size/2 - ($inkBounds.X + $inkBounds.Width/2)),
        [single]($y + $size/2 - ($inkBounds.Y + $inkBounds.Height/2))
    )
    $textPath.Transform($move)
    $g.FillPath($textBrush, $textPath)
    $move.Dispose(); $textPath.Dispose(); $textBrush.Dispose(); $font.Dispose(); $path.Dispose()
}

function DrawMini($g, [single]$x, [single]$y, [single]$width, [single]$height, [string]$kind) {
    $r = [Math]::Min($width,$height) * 0.168
    $path = RoundedPath $x $y $width $height $r
    $fill = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(205,240,245,244))
    $g.FillPath($fill,$path); $fill.Dispose()
    $rim = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(110,255,255,255)), 2
    $g.DrawPath($rim,$path); $rim.Dispose()
    $ink = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(235,19,32,34))
    $muted = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(155,19,32,34))
    if ($kind -eq 'weather') {
        $f1 = New-Object Drawing.Font 'Segoe UI', 22, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $f2 = New-Object Drawing.Font 'Segoe UI', 44, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $g.DrawString('SHANGHAI', $f1, $muted, $x+24, $y+22)
        $g.DrawString('37 C', $f2, $ink, $x+22, $y+56)
        $f1.Dispose(); $f2.Dispose()
        $g.FillEllipse($ink,$x+$width-78,$y+38,34,34)
    } elseif ($kind -eq 'calendar') {
        $f1 = New-Object Drawing.Font 'Segoe UI', 20, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $f2 = New-Object Drawing.Font 'Bahnschrift', 58, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $g.DrawString('JUL', $f1, $muted, $x+24, $y+22)
        $g.DrawString('17', $f2, $ink, $x+20, $y+48)
        $f1.Dispose(); $f2.Dispose()
    } else {
        $f1 = New-Object Drawing.Font 'Segoe UI', 21, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $g.DrawString('TASKS', $f1, $ink, $x+24, $y+22)
        for ($i=0; $i -lt 3; $i++) {
            $g.DrawEllipse((New-Object Drawing.Pen $muted,2),$x+27,$y+66+$i*31,14,14)
            $g.FillRectangle($muted,$x+54,$y+70+$i*31,$width-86,5)
        }
        $f1.Dispose()
    }
    $ink.Dispose(); $muted.Dispose(); $path.Dispose()
}

DrawWallpaper $g $w $h

$overlay = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(78,5,13,20))
$g.FillRectangle($overlay,0,0,$w,$h); $overlay.Dispose()

$labelFont = New-Object Drawing.Font 'Segoe UI Semibold', 17, ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
$labelBrush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(210,255,255,255))
$positions = @(90, 690, 1290)
$labels = @('REFERENCE WHITE','LIQUID GLASS','FROSTED GLASS')
$materials = @('reference','liquid','frosted')
for ($i=0; $i -lt 3; $i++) {
    $g.DrawString($labels[$i],$labelFont,$labelBrush,$positions[$i],72)
    DrawClock $g $positions[$i] 118 420 $materials[$i]
}

$smallLabel = New-Object Drawing.Font 'Segoe UI Semibold', 16, ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
$g.DrawString('SHARED CORNER LANGUAGE  /  16.8% RADIUS',$smallLabel,$labelBrush,90,610)
DrawMini $g 90 662 500 190 'weather'
DrawMini $g 650 662 250 250 'calendar'
DrawMini $g 960 662 750 250 'todo'

$footFont = New-Object Drawing.Font 'Segoe UI', 15, ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
$footBrush = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(170,255,255,255))
$g.DrawString('60 minute marks  /  optical centering  /  one continuous radius system',$footFont,$footBrush,90,1088)

$labelFont.Dispose(); $smallLabel.Dispose(); $labelBrush.Dispose(); $footFont.Dispose(); $footBrush.Dispose()
$g.Dispose()
$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output $out
