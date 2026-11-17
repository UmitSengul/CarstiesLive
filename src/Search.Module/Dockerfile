FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Search.Module/Search.Module.csproj", "src/Search.Module/"]
COPY ["src/Contracts/Contracts.csproj", "src/Contracts/"]
RUN dotnet restore "src/Search.Module/Search.Module.csproj"
COPY . .
WORKDIR "/src/src/SearchService"
RUN dotnet build "./Search.Module.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Search.Module.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Search.Module.dll"]
