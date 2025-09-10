FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY ["WebhookService.sln", "."]

# Copy project files
COPY ["src/WebhookService.Api/WebhookService.Api.csproj", "src/WebhookService.Api/"]
COPY ["src/WebhookService.Core/WebhookService.Core.csproj", "src/WebhookService.Core/"]
COPY ["src/WebhookService.Infrastructure/WebhookService.Infrastructure.csproj", "src/WebhookService.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/WebhookService.Api/WebhookService.Api.csproj"

# Copy source code
COPY . .

# Build the application
WORKDIR "/src/src/WebhookService.Api"
RUN dotnet build "WebhookService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WebhookService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WebhookService.Api.dll"]
