#!/bin/bash

echo "🚀 Starting FirstMake Desktop in Development Mode..."

# Start backend in background
echo "Starting .NET backend..."
cd src/Api
dotnet run --no-build &
BACKEND_PID=$!

# Wait for backend to start
sleep 3

# Start frontend in background
echo "Starting React frontend..."
cd ../UI
npm run dev &
FRONTEND_PID=$!

# Wait for frontend to start
sleep 2

# Start Electron
echo "Starting Electron..."
cd ../../desktop
NODE_ENV=development npm start

# Cleanup on exit
echo "Stopping services..."
kill $BACKEND_PID $FRONTEND_PID 2>/dev/null
