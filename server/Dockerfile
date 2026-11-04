FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY MapApp.Api/MapApp.Api.csproj MapApp.Api/
RUN dotnet restore MapApp.Api/MapApp.Api.csproj
COPY MapApp.Api/ MapApp.Api/
RUN dotnet publish MapApp.Api/MapApp.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MapApp.Api.dll"]
