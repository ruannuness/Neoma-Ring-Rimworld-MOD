Add-Type -AssemblyName System.Drawing

$texBase = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\FormgelCore\1.6\Textures\Things\Building"

function Make-Texture {
    param(
        [string]$folder,
        [string]$file,
        [int]$tier,
        [int[]]$bgRgb,
        [int[]]$iconRgb,
        [string]$shape
    )

    $w = 64; $h = 64
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg = [System.Drawing.Color]::FromArgb(235, $bgRgb[0],   $bgRgb[1],   $bgRgb[2])
    $ic = [System.Drawing.Color]::FromArgb(255, $iconRgb[0], $iconRgb[1], $iconRgb[2])
    $bd = [System.Drawing.Color]::FromArgb(180, [int]($iconRgb[0]*0.65), [int]($iconRgb[1]*0.65), [int]($iconRgb[2]*0.65))
    $gl = [System.Drawing.Color]::FromArgb(35,  $iconRgb[0], $iconRgb[1], $iconRgb[2])

    # Background panel
    $pad = 3
    $g.FillRectangle((New-Object System.Drawing.SolidBrush($bg)), $pad, $pad, $w-$pad*2, $h-$pad*2)

    # Top glow strip
    $g.FillRectangle((New-Object System.Drawing.SolidBrush($gl)), $pad+1, $pad+1, $w-$pad*2-2, 8)

    # Border
    $g.DrawRectangle((New-Object System.Drawing.Pen($bd, 1.5)), $pad, $pad, $w-$pad*2, $h-$pad*2)

    # Icon center position (shift up to leave room for tier dots)
    $cx = [int]($w / 2)
    $cy = [int](($h - 14) / 2) + 3

    $icBrush = New-Object System.Drawing.SolidBrush($ic)
    $icPen   = New-Object System.Drawing.Pen($ic, 3.5)
    $bgBrush = New-Object System.Drawing.SolidBrush($bg)

    switch ($shape) {

        "combat" {
            # Bold X — two crossing blades
            $p = New-Object System.Drawing.Pen($ic, 4.5)
            $g.DrawLine($p, $cx-12, $cy-12, $cx+12, $cy+12)
            $g.DrawLine($p, $cx+12, $cy-12, $cx-12, $cy+12)
            # Diamond centre highlight
            $pts = [System.Drawing.PointF[]] @(
                [System.Drawing.PointF]::new($cx,    $cy-5),
                [System.Drawing.PointF]::new($cx+5,  $cy),
                [System.Drawing.PointF]::new($cx,    $cy+5),
                [System.Drawing.PointF]::new($cx-5,  $cy)
            )
            $g.FillPolygon($icBrush, $pts)
        }

        "work" {
            # Wrench: shaft + two circular heads
            $g.FillRectangle($icBrush, $cx-3, $cy-13, 6, 22)
            $g.FillEllipse(  $icBrush, $cx-9, $cy-15, 18, 12)
            # Cutout to shape the wrench jaws
            $g.FillRectangle($bgBrush, $cx-6, $cy-12, 12, 8)
            # Bottom cap
            $g.FillEllipse($icBrush, $cx-5, $cy+5, 10, 8)
        }

        "optimizer" {
            # Hub with 4 arms — circuit / CPU feel
            $p3 = New-Object System.Drawing.Pen($ic, 3)
            $g.DrawEllipse($p3, $cx-8, $cy-8, 16, 16)
            $g.DrawLine($p3, $cx-8, $cy, $cx-15, $cy)
            $g.DrawLine($p3, $cx+8, $cy, $cx+15, $cy)
            $g.DrawLine($p3, $cx, $cy-8, $cx, $cy-15)
            $g.DrawLine($p3, $cx, $cy+8, $cx, $cy+15)
            # Filled centre
            $g.FillEllipse($icBrush, $cx-4, $cy-4, 8, 8)
            # Small squares at arm ends
            $g.FillRectangle($icBrush, $cx-18, $cy-3, 5, 6)
            $g.FillRectangle($icBrush, $cx+13, $cy-3, 5, 6)
            $g.FillRectangle($icBrush, $cx-3, $cy-20, 6, 5)
            $g.FillRectangle($icBrush, $cx-3, $cy+15, 6, 5)
        }

        "role" {
            # Outer diamond
            $outer = [System.Drawing.PointF[]] @(
                [System.Drawing.PointF]::new($cx,    $cy-15),
                [System.Drawing.PointF]::new($cx+15, $cy),
                [System.Drawing.PointF]::new($cx,    $cy+15),
                [System.Drawing.PointF]::new($cx-15, $cy)
            )
            $g.FillPolygon($icBrush, $outer)
            # Dark inner diamond (cutout)
            $inner = [System.Drawing.PointF[]] @(
                [System.Drawing.PointF]::new($cx,   $cy-7),
                [System.Drawing.PointF]::new($cx+7, $cy),
                [System.Drawing.PointF]::new($cx,   $cy+7),
                [System.Drawing.PointF]::new($cx-7, $cy)
            )
            $g.FillPolygon($bgBrush, $inner)
            # Bright centre dot
            $g.FillEllipse($icBrush, $cx-2, $cy-2, 4, 4)
        }

        "armor" {
            # Shield shape: rounded-top hexagon
            $shield = [System.Drawing.PointF[]] @(
                [System.Drawing.PointF]::new($cx-12, $cy-14),
                [System.Drawing.PointF]::new($cx+12, $cy-14),
                [System.Drawing.PointF]::new($cx+14, $cy-2),
                [System.Drawing.PointF]::new($cx,    $cy+15),
                [System.Drawing.PointF]::new($cx-14, $cy-2)
            )
            $g.FillPolygon($icBrush, $shield)
            # Dark inner shield (smaller)
            $shieldIn = [System.Drawing.PointF[]] @(
                [System.Drawing.PointF]::new($cx-7,  $cy-10),
                [System.Drawing.PointF]::new($cx+7,  $cy-10),
                [System.Drawing.PointF]::new($cx+8,  $cy-2),
                [System.Drawing.PointF]::new($cx,    $cy+9),
                [System.Drawing.PointF]::new($cx-8,  $cy-2)
            )
            $g.FillPolygon($bgBrush, $shieldIn)
            # Vertical slash in the inner
            $g.FillRectangle($icBrush, $cx-2, $cy-8, 4, 14)
        }

        "overclock" {
            # Lightning bolt
            $bolt = [System.Drawing.PointF[]] @(
                [System.Drawing.PointF]::new($cx+6,  $cy-15),
                [System.Drawing.PointF]::new($cx-2,  $cy-2),
                [System.Drawing.PointF]::new($cx+5,  $cy-2),
                [System.Drawing.PointF]::new($cx-6,  $cy+15),
                [System.Drawing.PointF]::new($cx+2,  $cy+1),
                [System.Drawing.PointF]::new($cx-4,  $cy+1)
            )
            $g.FillPolygon($icBrush, $bolt)
            # Glow effect: slightly larger transparent version
            $glowC = [System.Drawing.Color]::FromArgb(80, $iconRgb[0], $iconRgb[1], $iconRgb[2])
            $glowB = New-Object System.Drawing.SolidBrush($glowC)
            $boltG = [System.Drawing.PointF[]] @(
                [System.Drawing.PointF]::new($cx+8,  $cy-16),
                [System.Drawing.PointF]::new($cx-4,  $cy-2),
                [System.Drawing.PointF]::new($cx+7,  $cy-2),
                [System.Drawing.PointF]::new($cx-8,  $cy+16),
                [System.Drawing.PointF]::new($cx+4,  $cy+1),
                [System.Drawing.PointF]::new($cx-6,  $cy+1)
            )
            # Draw glow before bolt (order matters)
        }

        "versatility" {
            # Star / sparkle: 4-point star
            $p4 = New-Object System.Drawing.Pen($ic, 3)
            # Horizontal and vertical thick bars
            $g.FillRectangle($icBrush, $cx-14, $cy-3, 28, 6)
            $g.FillRectangle($icBrush, $cx-3, $cy-14, 6, 28)
            # Diagonal thin bars
            $p2 = New-Object System.Drawing.Pen($ic, 2.5)
            $g.DrawLine($p2, $cx-9, $cy-9, $cx+9, $cy+9)
            $g.DrawLine($p2, $cx+9, $cy-9, $cx-9, $cy+9)
            # White centre
            $g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220,255,255,255))), $cx-3, $cy-3, 6, 6)
        }
    }

    # Tier indicator dots at bottom
    $dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 255, 255, 255))
    $dotSize  = 5
    $dotGap   = 9
    $dotY     = $h - $pad - $dotSize - 3
    $totalW   = ($tier - 1) * $dotGap
    $startX   = $cx - $totalW / 2.0

    for ($i = 0; $i -lt $tier; $i++) {
        $dotX = $startX + $i * $dotGap - $dotSize / 2.0
        $g.FillEllipse($dotBrush, [float]$dotX, [float]$dotY, [float]$dotSize, [float]$dotSize)
    }

    # Save
    $dir = Join-Path $texBase $folder
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $out = Join-Path $dir "$file.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

    $g.Dispose(); $bmp.Dispose()
    Write-Host "OK  Things/Building/$folder/$file.png"
}

# ─── Combat (orange-red) ─────────────────────────────────────────────────────
Make-Texture "AuxCombat" "TierI"   1  @(52,18, 4) @(255,100,30)  "combat"
Make-Texture "AuxCombat" "TierII"  2  @(62,23, 6) @(255,140,55)  "combat"
Make-Texture "AuxCombat" "TierIII" 3  @(72,28, 8) @(255,185,80)  "combat"

# ─── Work (blue) ─────────────────────────────────────────────────────────────
Make-Texture "AuxWork"  "TierI"    1  @( 5,20,58) @( 50,145,255) "work"
Make-Texture "AuxWork"  "TierII"   2  @( 8,26,70) @( 85,175,255) "work"

# ─── Optimizer (green) ───────────────────────────────────────────────────────
Make-Texture "AuxOptimizer" "TierI"   1  @( 5,46,20) @(40,205, 80) "optimizer"
Make-Texture "AuxOptimizer" "TierII"  2  @( 7,56,25) @(65,225,100) "optimizer"
Make-Texture "AuxOptimizer" "TierIII" 3  @(10,66,30) @(90,245,125) "optimizer"

# ─── Role (purple) ───────────────────────────────────────────────────────────
Make-Texture "AuxRole" "TierI"    1  @(36, 5,62) @(185, 80,255) "role"
Make-Texture "AuxRole" "TierII"   2  @(46, 8,78) @(205,105,255) "role"
Make-Texture "AuxRole" "TierIII"  3  @(56,10,95) @(225,135,255) "role"

# Armor: silver/white on dark steel-gray
Make-Texture "AuxArmor"      "TierI"   1  @(28,35,42) @(180,200,220) "armor"
Make-Texture "AuxArmor"      "TierII"  2  @(32,40,48) @(210,225,240) "armor"
Make-Texture "AuxArmor"      "TierIII" 3  @(36,46,56) @(240,250,255) "armor"

# Overclock: deep red/crimson on near-black
Make-Texture "AuxOverclock"  "TierI"   1  @(50, 5, 5) @(255, 40, 20)  "overclock"
Make-Texture "AuxOverclock"  "TierII"  2  @(60, 5, 5) @(255, 60, 30)  "overclock"
Make-Texture "AuxOverclock"  "TierIII" 3  @(70, 5, 5) @(255, 90, 40)  "overclock"

# Versatility: teal/cyan on dark teal
Make-Texture "AuxVersatility" "TierI"  1  @( 5,40,45) @(40,210,220)  "versatility"
Make-Texture "AuxVersatility" "TierII" 2  @( 6,50,56) @(60,235,245)  "versatility"

# ─── Archotech tier — deep amber bg, bright gold icon, 4 dots ────────────────
$archoBg   = @(45,32, 5)
$archoIcon = @(255,215,50)
Make-Texture "AuxCombat"      "TierArchotech" 4  $archoBg $archoIcon "combat"
Make-Texture "AuxWork"        "TierArchotech" 4  $archoBg $archoIcon "work"
Make-Texture "AuxOptimizer"   "TierArchotech" 4  $archoBg $archoIcon "optimizer"
Make-Texture "AuxRole"        "TierArchotech" 4  $archoBg $archoIcon "role"
Make-Texture "AuxArmor"       "TierArchotech" 4  $archoBg $archoIcon "armor"
Make-Texture "AuxOverclock"   "TierArchotech" 4  $archoBg $archoIcon "overclock"
Make-Texture "AuxVersatility" "TierArchotech" 4  $archoBg $archoIcon "versatility"

# ─── Archotech Terminal (2×2 building, 128×128) ───────────────────────────────
function Make-Terminal {
    $w = 128; $h = 128
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bgColor   = [System.Drawing.Color]::FromArgb(240, 45, 32,  5)
    $bdColor   = [System.Drawing.Color]::FromArgb(200,180,140, 20)
    $goldColor = [System.Drawing.Color]::FromArgb(255,255,215, 50)
    $glowColor = [System.Drawing.Color]::FromArgb( 60,255,215, 50)

    $g.FillRectangle((New-Object System.Drawing.SolidBrush($bgColor)),  4,  4, 120, 120)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush($glowColor)), 6,  6, 116, 116)
    $g.DrawRectangle((New-Object System.Drawing.Pen($bdColor, 2.5)),     4,  4, 120, 120)

    $icBrush = New-Object System.Drawing.SolidBrush($goldColor)
    $icPen   = New-Object System.Drawing.Pen($goldColor, 3)
    $cx = 64; $cy = 64

    # Central ring
    $g.DrawEllipse($icPen, $cx-22, $cy-22, 44, 44)
    # Filled hub
    $g.FillEllipse($icBrush, $cx-8, $cy-8, 16, 16)

    # 6 arms with diamond tips
    for ($a = 0; $a -lt 6; $a++) {
        $ang = $a * 60.0 * [Math]::PI / 180.0
        $x1  = [float]($cx + 22 * [Math]::Cos($ang))
        $y1  = [float]($cy + 22 * [Math]::Sin($ang))
        $x2  = [float]($cx + 50 * [Math]::Cos($ang))
        $y2  = [float]($cy + 50 * [Math]::Sin($ang))
        $g.DrawLine($icPen, $x1, $y1, $x2, $y2)

        $dx  = [float]($cx + 53 * [Math]::Cos($ang))
        $dy  = [float]($cy + 53 * [Math]::Sin($ang))
        $nx  = [float]([Math]::Cos($ang))
        $ny  = [float]([Math]::Sin($ang))
        $tx  = [float](-[Math]::Sin($ang))
        $ty  = [float]( [Math]::Cos($ang))
        $tip = [System.Drawing.PointF[]] @(
            [System.Drawing.PointF]::new($dx + 7*$nx,  $dy + 7*$ny),
            [System.Drawing.PointF]::new($dx + 4*$tx,  $dy + 4*$ty),
            [System.Drawing.PointF]::new($dx - 7*$nx,  $dy - 7*$ny),
            [System.Drawing.PointF]::new($dx - 4*$tx,  $dy - 4*$ty)
        )
        $g.FillPolygon($icBrush, $tip)
    }

    # Corner accent diamonds
    foreach ($corner in @(@(18,18), @(110,18), @(18,110), @(110,110))) {
        $px = [float]$corner[0]; $py = [float]$corner[1]
        $cd = [System.Drawing.PointF[]] @(
            [System.Drawing.PointF]::new($px,    $py-7),
            [System.Drawing.PointF]::new($px+5,  $py),
            [System.Drawing.PointF]::new($px,    $py+7),
            [System.Drawing.PointF]::new($px-5,  $py)
        )
        $g.FillPolygon($icBrush, $cd)
    }

    $out = Join-Path $texBase "ArchotechTerminal.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "OK  Things/Building/ArchotechTerminal.png"
}

# ─── Archotech Shard item (64×64) ────────────────────────────────────────────
function Make-ShardItem {
    $itemBase = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\FormgelCore\1.6\Textures\Things\Item\Resource"
    if (-not (Test-Path $itemBase)) { New-Item -ItemType Directory -Force $itemBase | Out-Null }

    $w = 64; $h = 64
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $goldColor  = [System.Drawing.Color]::FromArgb(255, 255, 215,  50)
    $darkGold   = [System.Drawing.Color]::FromArgb(255, 160, 120,  10)
    $glowColor  = [System.Drawing.Color]::FromArgb( 90, 255, 215,  50)
    $brightLine = [System.Drawing.Color]::FromArgb(220, 255, 255, 200)

    # Outer glow
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($glowColor)), 14, 8, 36, 48)

    # Crystal body
    $crystal = [System.Drawing.PointF[]] @(
        [System.Drawing.PointF]::new(32,  6),
        [System.Drawing.PointF]::new(45, 18),
        [System.Drawing.PointF]::new(47, 36),
        [System.Drawing.PointF]::new(36, 54),
        [System.Drawing.PointF]::new(22, 54),
        [System.Drawing.PointF]::new(15, 35),
        [System.Drawing.PointF]::new(19, 17)
    )
    $g.FillPolygon((New-Object System.Drawing.SolidBrush($goldColor)), $crystal)

    # Inner facet (darker)
    $facet = [System.Drawing.PointF[]] @(
        [System.Drawing.PointF]::new(32, 14),
        [System.Drawing.PointF]::new(41, 26),
        [System.Drawing.PointF]::new(37, 43),
        [System.Drawing.PointF]::new(25, 44),
        [System.Drawing.PointF]::new(21, 28)
    )
    $g.FillPolygon((New-Object System.Drawing.SolidBrush($darkGold)), $facet)

    # Highlight line
    $g.DrawLine((New-Object System.Drawing.Pen($brightLine, 1.5)), [float]32, [float]10, [float]32, [float]50)

    $out = Join-Path $itemBase "ArchotechShard.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "OK  Things/Item/Resource/ArchotechShard.png"
}

Make-Terminal
Make-ShardItem

Write-Host ""
Write-Host "Done - 28 textures generated."
