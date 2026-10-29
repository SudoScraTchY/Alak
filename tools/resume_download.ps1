$url = "https://huggingface.co/bartowski/SmolLM2-135M-Instruct-GGUF/resolve/main/SmolLM2-135M-Instruct-Q4_K_M.gguf"
$file = "dist/models/smollm2-135m-instruct-q4_k_m.gguf"
$target = 105454432

while ($true) {
    if (Test-Path $file) {
        $len = (Get-Item $file).Length
        if ($len -ge $target) {
            Write-Host "Download finished: $len bytes"
            break
        }
        Write-Host "Current size: $len / $target ($([math]::Round($len/$target*100, 1))%). Resuming..."
    }
    curl.exe -sL -C - "$url" -o "$file"
    Start-Sleep -Seconds 1
}
