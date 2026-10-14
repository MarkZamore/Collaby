FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:a04d1c1d2d26119049494057d80ea6cda25bbd8aef7c444a1fc1ef874fd3955b AS base
WORKDIR /app
EXPOSE 5004

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "./"]
COPY ["services/RealtimeHost/RealtimeHost.csproj", "services/RealtimeHost/"]
COPY ["services/MessengerService/MessengerService.csproj", "services/MessengerService/"]
COPY ["services/CallService/CallService.csproj", "services/CallService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/Collaby.ServiceDefaults.csproj", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/Collaby.EventBus.csproj", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/Collaby.InternalContracts.csproj", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
RUN dotnet restore "services/RealtimeHost/RealtimeHost.csproj"
COPY ["services/RealtimeHost/", "services/RealtimeHost/"]
COPY ["services/MessengerService/", "services/MessengerService/"]
COPY ["services/CallService/", "services/CallService/"]
COPY ["platform/dotnet/Collaby.ServiceDefaults/", "platform/dotnet/Collaby.ServiceDefaults/"]
COPY ["platform/dotnet/Collaby.EventBus/", "platform/dotnet/Collaby.EventBus/"]
COPY ["platform/dotnet/Collaby.InternalContracts/", "platform/dotnet/Collaby.InternalContracts/"]
COPY ["platform/contracts/events/", "platform/contracts/events/"]
WORKDIR /src/services/RealtimeHost
RUN dotnet build "RealtimeHost.csproj" -c Release -m:1

FROM build AS publish
RUN dotnet publish "RealtimeHost.csproj" -c Release -m:1 -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RealtimeHost.dll"]
