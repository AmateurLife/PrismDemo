# 60-strip-algorithm.ps1 -- empty the algorithm region bodies in AMainService.cs / BMainService.cs
# (regions kept) and delete the algorithm source files that only that code referenced.
#
# Strategy: match every INNERMOST '#region ... #endregion' pair (the body pattern cannot contain
# another '#region', so wrapper regions are skipped). If the body contains an algorithm call,
# replace the body with a single placeholder comment at the region's indentation.
# Idempotent / re-runnable.
# ASCII-only script body; the replacement text comes from placeholder.txt (UTF-8).
$ErrorActionPreference = 'Stop'
$outDir = $PSScriptRoot
$dstRoot = Split-Path $outDir -Parent
$utf8 = New-Object System.Text.UTF8Encoding($false)

$placeholder = ([System.IO.File]::ReadAllText((Join-Path $outDir 'placeholder.txt'), $utf8)).Trim()

$targets = @(
    'PrismDemo.A\Services\AMainService.cs',
    'PrismDemo.B\Services\BMainService.cs'
)
$algorithmCalls = 'DataFilter\.FilterMean|ProcessModel\.|Feedback\.|UnitRateConvert\.|_opcService\.WriteNode\(_[ab]Data\.GetField'
$regionRegex = New-Object System.Text.RegularExpressions.Regex(
    '(?s)(?<open>[ \t]*#region[^\r\n]*\r?\n)(?<body>(?:(?!#region)(?!#endregion).)*?)(?<close>[ \t]*#endregion)')

foreach ($rel in $targets) {
    $target = Join-Path $dstRoot $rel
    $bytes = [System.IO.File]::ReadAllBytes($target)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $text = $utf8.GetString($bytes, 0, $bytes.Length)

    $eval = [System.Text.RegularExpressions.MatchEvaluator]{
        param($m)
        if (-not ($m.Groups['body'].Value -match $algorithmCalls)) { return $m.Value }
        $indent = [regex]::Match($m.Groups['close'].Value, '^[ \t]*').Value
        return $m.Groups['open'].Value + $indent + $placeholder + "`r`n" + $m.Groups['close'].Value
    }
    $new = $regionRegex.Replace($text, $eval)
    $new = $new -replace '(?m)^[ \t]*private FeedbackCount count\d = new FeedbackCount\(\d\);\r?\n', ''

    if ($new -ne $text) {
        [System.IO.File]::WriteAllText($target, $new, (New-Object System.Text.UTF8Encoding($hasBom)))
        "$rel updated"
    } else {
        "$rel unchanged"
    }
}

# algorithm source files referenced only by the removed code
$kill = @(
    'PrismDemo.A\Services\ProcessModel.cs',
    'PrismDemo.A\Services\Feedback.cs',
    'PrismDemo.A\Services\UnitRateConvert.cs',
    'PrismDemo.A\Services\LinearInterpolation.cs',
    'PrismDemo.A\Models\FeedbackCount.cs',
    'PrismDemo.B\Services\ProcessModel.cs',
    'PrismDemo.B\Services\Feedback.cs',
    'PrismDemo.B\Services\UnitRateConvert.cs',
    'PrismDemo.B\Services\LinearInterpolation.cs',
    'PrismDemo.B\Models\FeedbackCount.cs'
)
foreach ($rel in $kill) {
    $p = Join-Path $dstRoot $rel
    if (Test-Path $p) { Remove-Item $p -Force; "deleted: $rel" }
}
