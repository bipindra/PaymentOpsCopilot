#!/bin/bash

# Payment Ops Console - Local Run Script (Bash)

echo "Starting Payment Ops Console..."

# Check if Docker is running
echo ""
echo "Checking Docker..."
if ! docker ps > /dev/null 2>&1; then
    echo "Docker is not running. Please start Docker."
    exit 1
fi
echo "Docker is running"

# Start Qdrant
echo ""
echo "Starting Qdrant..."
docker-compose up -d
sleep 3

# Check if Qdrant is healthy
echo "Waiting for Qdrant..."
for i in {1..10}; do
    if curl -s http://localhost:6333/health > /dev/null 2>&1; then
        echo "Qdrant is healthy"
        break
    fi
    sleep 1
done

# Check for OpenAI API key
if [ -z "$OPENAI_API_KEY" ]; then
    echo ""
    echo "WARNING: OPENAI_API_KEY environment variable is not set."
    echo "Set it with: export OPENAI_API_KEY='your-key-here'"
    echo "Or (recommended) set a .NET User Secret:"
    echo "  cd src/PaymentOps.Backend && dotnet user-secrets set \"OpenAI:ApiKey\" \"your-key-here\""
    echo "  cd src/PaymentOps.Backend && dotnet user-secrets set \"Qdrant:BaseUrl\" \"http://localhost:6333\""
fi

# Start backend in background
echo ""
echo "Starting Backend..."
cd src/PaymentOps.Backend
dotnet run &
BACKEND_PID=$!
cd ../..

# Wait a bit for backend to start
sleep 5

# Start frontend in background
echo "Starting Frontend..."
cd frontend/payment-ops-ui
if [ ! -d "node_modules" ]; then
    npm install
fi
ng serve --open &
FRONTEND_PID=$!
cd ../..

echo ""
echo "Services starting..."
echo "Backend: http://localhost:5000"
echo "Frontend: http://localhost:4200"
echo "Qdrant: http://localhost:6333"
echo ""
echo "Press Ctrl+C to stop all services"

# Wait for user interrupt
trap "echo ''; echo 'Stopping services...'; kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; docker-compose down; echo 'Services stopped'; exit" INT TERM

wait
