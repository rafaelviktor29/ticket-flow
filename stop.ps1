Write-Host "====================================="
Write-Host " Stopping TicketFlow Services"
Write-Host "====================================="

# Stop and remove all running containers defined in docker-compose
docker compose down

Write-Host "All services have been stopped."