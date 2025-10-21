@echo off
echo Building FirstMake Desktop App...

echo.
echo Step 1/3: Building .NET backend...
cd ..\src\Api
dotnet publish -c Release -o ..\..\desktop\backend-build

echo.
echo Step 2/3: Building React frontend...
cd ..\UI
call npm run build

echo.
echo Step 3/3: Packaging Electron app...
cd ..\..\desktop
call npm install
call npm run build

echo.
echo Build complete! Installers are in: desktop\dist\
pause
