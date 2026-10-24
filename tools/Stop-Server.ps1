$ErrorActionPreference = 'Stop'
Invoke-WebRequest -Method Post -Uri 'http://127.0.0.1:5080/api/dev/shutdown' -Headers @{ 'X-TuTien-Local-Control' = 'shutdown' } | Select-Object StatusCode
Write-Output 'Shutdown requested. Check host logs for Base server stopped.'
