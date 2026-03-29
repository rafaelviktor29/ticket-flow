# Run both API and Worker for local development
# Usage: ./run-dev.ps1

# Start Worker as a background job
Start-Job -Name TicketFlow.Worker -ScriptBlock { dotnet run --project src\TicketFlow.Worker } | Out-Null
Write-Host "Worker started as background job (TicketFlow.Worker)."

# Start API in foreground (so logs are visible in this terminal)
dotnet run --project src\TicketFlow.API

# When API exits, stop and remove the Worker job
Get-Job -Name TicketFlow.Worker | Stop-Job -Force -ErrorAction SilentlyContinue
Get-Job -Name TicketFlow.Worker | Remove-Job -Force -ErrorAction SilentlyContinue
Write-Host "Worker stopped."
