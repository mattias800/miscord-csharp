#!/bin/bash

# Development login screen testing script
# Usage: ./dev-login.sh
# Note: Run dev-backend.sh first
# This starts the client WITHOUT auto-login so you can test the welcome/login screens

# Set VLC environment variables for audio playback on macOS
if [[ "$OSTYPE" == "darwin"* ]]; then
    export VLC_PLUGIN_PATH="/Applications/VLC.app/Contents/MacOS/plugins"
    export DYLD_LIBRARY_PATH="/Applications/VLC.app/Contents/MacOS/lib:$DYLD_LIBRARY_PATH"
fi

SERVER_URL="http://localhost:5117"
CLIENT_PROJECT="src/Snacka.Client/Snacka.Client.csproj"

echo "=== Snacka Client (Login Screen Testing) ==="
echo ""

# Check if server is running
echo "Checking if server is running..."
if ! curl -s "$SERVER_URL/api/health" > /dev/null 2>&1; then
    echo "Server is not running at $SERVER_URL"
    echo "Please start the backend first with: ./dev-backend.sh"
    exit 1
fi
echo "Server is ready!"
echo ""

# Build client
echo "Building client..."
dotnet build "$CLIENT_PROJECT" --verbosity quiet
if [ $? -ne 0 ]; then
    echo "Client build failed!"
    exit 1
fi

echo "Build complete."
echo ""

# Start client with just the server URL (no auto-login)
echo "Starting client without auto-login..."
dotnet run --project "$CLIENT_PROJECT" --no-build -- \
    --server "$SERVER_URL" \
    --title "Snacka - Login Testing" \
    --profile login-test
