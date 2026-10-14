FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8c0b6857eab7b2aa57884c839bf4678414606bd7d17370f18a842ac5cf414711 AS base
WORKDIR /app
EXPOSE 5002

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:c0790639332692a0d56cdd81ed581cfd24d040d9839764c138994866df89a3b6 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["services/ManagementService/ManagementService.csproj", "services/ManagementService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/Collaby.ServiceDefaults.csproj", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/Collaby.EventBus.csproj", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/Collaby.InternalContracts.csproj", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
RUN dotnet restore "services/ManagementService/ManagementService.csproj"
COPY ["services/ManagementService/", "services/ManagementService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
COPY ["platform/contracts/runtime/service-catalog.json", "platform/contracts/runtime/"]
COPY ["frontend/src/shared/locales/", "frontend/src/shared/locales/"]
WORKDIR /src/services/ManagementService
RUN dotnet build "ManagementService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ManagementService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ManagementService.dll"]
