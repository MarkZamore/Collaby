FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a04d1c1d2d26119049494057d80ea6cda25bbd8aef7c444a1fc1ef874fd3955b AS base
WORKDIR /app
EXPOSE 5003

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["services/WorkspaceHost/WorkspaceHost.csproj", "services/WorkspaceHost/"]
COPY ["services/ManagementService/ManagementService.csproj", "services/ManagementService/"]
COPY ["services/AnalyticsService/AnalyticsService.csproj", "services/AnalyticsService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/Collaby.ServiceDefaults.csproj", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/Collaby.EventBus.csproj", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/Collaby.InternalContracts.csproj", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
RUN dotnet restore "services/WorkspaceHost/WorkspaceHost.csproj"
COPY ["services/WorkspaceHost/", "services/WorkspaceHost/"]
COPY ["services/ManagementService/", "services/ManagementService/"]
COPY ["services/AnalyticsService/", "services/AnalyticsService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
COPY ["platform/contracts/runtime/service-catalog.json", "platform/contracts/runtime/"]
COPY ["frontend/src/shared/locales/", "frontend/src/shared/locales/"]
WORKDIR /src/services/WorkspaceHost
RUN dotnet build "WorkspaceHost.csproj" -c Release -m:1

FROM build AS publish
RUN dotnet publish "WorkspaceHost.csproj" -c Release -m:1 -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkspaceHost.dll"]
