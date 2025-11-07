#!/bin/bash

echo "🚀 Starting FirstMake Development Environment..."

# Kill any existing processes
echo "Stopping any existing services..."
pkill -f "dotnet.*Api" 2>/dev/null
pkill -f "npm run dev" 2>/dev/null
pkill -f "vite" 2>/dev/null
sleep 2

# Start backend
echo "📦 Starting Backend API..."
cd /workspaces/First-make/src/Api
nohup dotnet run > /tmp/backend.log 2>&1 &
BACKEND_PID=$!
echo "Backend started (PID: $BACKEND_PID)"

# Start frontend
echo "🎨 Starting Frontend..."
cd /workspaces/First-make/src/UI
nohup npm run dev > /tmp/frontend.log 2>&1 &
FRONTEND_PID=$!
echo "Frontend started (PID: $FRONTEND_PID)"

# Wait for services to start
echo "⏳ Waiting for services to start..."
sleep 6

# Check status
echo ""
echo "=== Status ==="
if ss -tlnp 2>/dev/null | grep -q 5085; then
    echo "✅ Backend API: http://localhost:5085"
else
    echo "❌ Backend failed to start. Check: tail -20 /tmp/backend.log"
fi

if ss -tlnp 2>/dev/null | grep -q 5173; then
    echo "✅ Frontend UI: http://localhost:5173"
else
    echo "❌ Frontend failed to start. Check: tail -20 /tmp/frontend.log"
fi

echo ""
echo "📝 Logs:"
echo "  Backend:  tail -f /tmp/backend.log"
echo "  Frontend: tail -f /tmp/frontend.log"
echo ""
echo "🛑 To stop services: pkill -f 'dotnet.*Api'; pkill -f 'npm run dev'"
