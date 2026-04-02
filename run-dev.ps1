Write-Host "====================================="
Write-Host " TicketFlow - Development Mode"
Write-Host "====================================="

# Step 1: Start infrastructure services (PostgreSQL and RabbitMQ)
Write-Host "Starting PostgreSQL and RabbitMQ..."
docker compose up -d postgres rabbitmq

# Wait a few seconds to ensure services are ready
Start-Sleep -Seconds 5

# Step 2: Start API in a new terminal window
Write-Host "Starting API..."
Start-Process powershell -ArgumentList "dotnet run --project src/TicketFlow.API"

# Small delay to ensure API starts before workers
Start-Sleep -Seconds 3

# Step 3: Start Worker in a new terminal window
Write-Host "Starting Worker..."
Start-Process powershell -ArgumentList "dotnet run --project src/TicketFlow.Worker"

# Display useful URLs
Write-Host ""
Write-Host "API: http://localhost:54049"
Write-Host "Swagger: http://localhost:54049/swagger"
Write-Host "RabbitMQ: http://localhost:15672 (guest/guest)"
Write-Host ""
Write-Host "Development environment started successfully."