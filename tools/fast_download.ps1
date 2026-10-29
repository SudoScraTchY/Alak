$url = "https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF/resolve/main/SmolLM2-135M-Instruct-Q4_K_M.gguf"
$output = "dist/models/smollm2-135m-instruct-q4_k_m.gguf"
$totalSize = 105454432
$parts = 6
$chunkSize = [math]::Ceiling($totalSize / $parts)

if (!(Test-Path "dist/models")) {
    New-Item -ItemType Directory -Path "dist/models" -Force | Out-Null
}

$jobs = @()
for ($i = 0; $i -lt $parts; $i++) {
    $start = $i * $chunkSize
    $end = [math]::Min(($i + 1) * $chunkSize - 1, $totalSize - 1)
    $partFile = "dist/models/part$i.tmp"
    
    $cmd = "curl.exe -sL -r $start-$end `"$url`" -o `"$partFile`""
    $jobs += Start-Process -FilePath "curl.exe" -ArgumentList "-sL", "-r", "$start-$end", "$url", "-o", "$partFile" -PassThru
}

$jobs | Wait-Process

# Combine parts
$outStream = [System.IO.File]::Create($output)
for ($i = 0; $i -lt $parts; $i++) {
    $partFile = "dist/models/part$i.tmp"
    if (Test-Path $partFile) {
        $inBytes = [System.IO.File]::ReadAllBytes($partFile)
        $outStream.Write($inBytes, 0, $inBytes.Length)
        Remove-Item $partFile -Force
    }
}
$outStream.Dispose()

Write-Host "Download complete: $output size: $((Get-Item $output).Length)"
