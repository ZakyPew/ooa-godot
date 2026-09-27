$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\import_oracles\Write-Png.ps1')

$path = Join-Path ([IO.Path]::GetTempPath()) "ooa-png-writer-$([Guid]::NewGuid().ToString('N')).png"
function Test-ByteSequence([byte[]]$Actual, [byte[]]$Expected) {
    if ($Actual.Length -ne $Expected.Length) { return $false }
    for ($index = 0; $index -lt $Actual.Length; $index++) {
        if ($Actual[$index] -ne $Expected[$index]) { return $false }
    }
    return $true
}

try {
    [byte[]]$expectedPixels = @(
        255, 0, 0, 255, 0, 255, 0, 128,
        0, 0, 255, 255, 12, 34, 56, 0
    )
    Write-RgbaPng $path 2 2 $expectedPixels
    [byte[]]$png = [IO.File]::ReadAllBytes($path)
    [byte[]]$signature = @(0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a)
    if (-not (Test-ByteSequence $png[0..7] $signature)) {
        throw 'PNG signature mismatch.'
    }

    $idat = [IO.MemoryStream]::new()
    try {
        $offset = 8
        while ($offset -lt $png.Length) {
            $length = [int]([uint32]$png[$offset] -shl 24 -bor
                [uint32]$png[$offset + 1] -shl 16 -bor
                [uint32]$png[$offset + 2] -shl 8 -bor
                [uint32]$png[$offset + 3])
            $type = [Text.Encoding]::ASCII.GetString($png, $offset + 4, 4)
            if ($type -eq 'IDAT') { $idat.Write($png, $offset + 8, $length) }
            $offset += 12 + $length
            if ($type -eq 'IEND') { break }
        }
        $compressed = [IO.MemoryStream]::new($idat.ToArray())
        $decompressed = [IO.MemoryStream]::new()
        $decoder = [IO.Compression.ZLibStream]::new(
            $compressed, [IO.Compression.CompressionMode]::Decompress)
        try { $decoder.CopyTo($decompressed) }
        finally {
            $decoder.Dispose()
            $compressed.Dispose()
        }

        [byte[]]$expectedScanlines = @(
            0, 255, 0, 0, 255, 0, 255, 0, 128,
            0, 0, 0, 255, 255, 12, 34, 56, 0
        )
        if (-not (Test-ByteSequence $decompressed.ToArray() $expectedScanlines)) {
            throw 'Decoded PNG pixels differ from the supplied RGBA buffer.'
        }
        $decompressed.Dispose()
    }
    finally { $idat.Dispose() }
    Write-Host 'Portable PNG writer test passed.'
}
finally {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
}
