FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:8c0b6857eab7b2aa57884c839bf4678414606bd7d17370f18a842ac5cf414711 AS base
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:c0790639332692a0d56cdd81ed581cfd24d040d9839764c138994866df89a3b6 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["services/IdentityService/IdentityService.csproj", "services/IdentityService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/Collaby.ServiceDefaults.csproj", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/Collaby.EventBus.csproj", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/Collaby.InternalContracts.csproj", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
RUN dotnet restore "services/IdentityService/IdentityService.csproj"
COPY ["services/IdentityService/", "services/IdentityService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
COPY ["platform/contracts/runtime/service-catalog.json", "platform/contracts/runtime/"]
WORKDIR /src/services/IdentityService
RUN dotnet build "IdentityService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IdentityService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IdentityService.dll"]
