# Multi-stage Dockerfile for FirstMake Agent

# Stage 1: Build .NET Backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dotnet-build
WORKDIR /src

# Copy solution and project files
COPY FirstMake.sln ./
COPY src/Api/Api.csproj ./src/Api/
COPY src/AiGateway/AiGateway.csproj ./src/AiGateway/
COPY src/Core.Engine/Core.Engine.csproj ./src/Core.Engine/
COPY tests/Core.Engine.Tests/Core.Engine.Tests.csproj ./tests/Core.Engine.Tests/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ ./src/
COPY tests/ ./tests/

# Build and publish
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/api --no-restore
RUN dotnet publish src/AiGateway/AiGateway.csproj -c Release -o /app/aigateway --no-restore

# Stage 2: Build React Frontend
FROM node:20-alpine AS node-build
WORKDIR /app

# Copy package files
COPY src/UI/package*.json ./

# Install dependencies
RUN npm ci --production=false

# Copy source code
COPY src/UI/ ./

# Build production
RUN npm run build

# Stage 3: Runtime - API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api-runtime
WORKDIR /app

# Install dependencies
RUN apt-get update && apt-get install -y \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy API build
COPY --from=dotnet-build /app/api ./

# Create data directory
RUN mkdir -p /data

# Expose port
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__DefaultConnection="Data Source=/data/firstmake.db"

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "Api.dll"]

# Stage 4: Runtime - AI Gateway
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS aigateway-runtime
WORKDIR /app

# Install Tesseract OCR
RUN apt-get update && apt-get install -y \
    tesseract-ocr \
    tesseract-ocr-bul \
    tesseract-ocr-eng \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy AI Gateway build
COPY --from=dotnet-build /app/aigateway ./

# Expose port
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV Tesseract__DataPath=/usr/share/tesseract-ocr/5/tessdata

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "AiGateway.dll"]

# Stage 5: Runtime - Nginx with UI
FROM nginx:alpine AS ui-runtime

# Copy UI build
COPY --from=node-build /app/dist /usr/share/nginx/html

# Copy nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:80/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
