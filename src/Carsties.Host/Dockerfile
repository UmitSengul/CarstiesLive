FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
# C2: 8081 kaldirilacak (gRPC yerine surec ici IAuctionLookup, tek port).
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Carsties.Host/Carsties.Host.csproj", "src/Carsties.Host/"]
COPY ["src/Auction.Module/Auction.Module.csproj", "src/Auction.Module/"]
COPY ["src/Bid.Module/Bid.Module.csproj", "src/Bid.Module/"]
COPY ["src/Contracts/Contracts.csproj", "src/Contracts/"]
RUN dotnet restore "src/Carsties.Host/Carsties.Host.csproj"
COPY . .
WORKDIR "/src/src/Carsties.Host"
RUN dotnet build "./Carsties.Host.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Carsties.Host.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Carsties.Host.dll"]
