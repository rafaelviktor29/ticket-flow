Write-Host "====================================="
Write-Host " TicketFlow - Full Docker Mode"
Write-Host "====================================="

# Ask user how many worker instances should be started
$workers = Read-Host "How many workers do you want to run?"

# Default to 2 workers if no input is provided
if (-not $workers) {
    $workers = 2
}

Write-Host "Starting system with $workers worker(s)..."

# Build images and start all services with scaling
docker compose up --build --scale worker=$workers

# Display access information
Write-Host ""
Write-Host "API: http://localhost:54049"
Write-Host "RabbitMQ: http://localhost:15672 (guest/guest)"