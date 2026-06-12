#!/bin/bash
# Start QBIS — Backend (Azure Functions) + Frontend (React)

set -e

ROOT="$(cd "$(dirname "$0")" && pwd)"

echo "Starting QBIS Backend..."
cd "$ROOT/backend"
dotnet build --nologo
func start &
BACKEND_PID=$!

echo "Waiting for backend to start..."
sleep 8

echo "Starting QBIS Frontend..."
cd "$ROOT/frontend"
npm start &
FRONTEND_PID=$!

echo ""
echo "═══════════════════════════════════════════"
echo "  QBIS is running"
echo "  Backend:   http://localhost:7071/api"
echo "  Frontend:  http://localhost:3000"
echo "═══════════════════════════════════════════"
echo ""
echo "Press Ctrl+C to stop both"

trap "echo ''; echo 'Stopping QBIS...'; kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit 0" INT
wait
