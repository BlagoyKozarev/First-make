#!/bin/bash

echo "🔨 Building FirstMake Desktop App..."

# Step 1: Build backend
echo ""
echo "📦 Step 1/3: Building .NET backend..."
cd ../src/Api
dotnet publish -c Release -o ../../desktop/backend-build

# Step 2: Build frontend
echo ""
echo "🎨 Step 2/3: Building React frontend..."
cd ../UI
npm run build

# Step 3: Package Electron app
echo ""
echo "📦 Step 3/3: Packaging Electron app..."
cd ../../desktop
npm install
npm run build

echo ""
echo "✅ Build complete! Installers are in: desktop/dist/"
