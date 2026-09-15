FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY WorkoutCompanionServer.sln ./
COPY Directory.Build.props global.json NuGet.config ./
COPY src/WorkoutCompanion.Server/WorkoutCompanion.Server.csproj src/WorkoutCompanion.Server/
COPY tests/WorkoutCompanion.Server.Tests/WorkoutCompanion.Server.Tests.csproj tests/WorkoutCompanion.Server.Tests/
RUN dotnet restore WorkoutCompanionServer.sln

COPY . .
RUN dotnet publish src/WorkoutCompanion.Server/WorkoutCompanion.Server.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    WORKOUT_DATA_DIRECTORY=/data
EXPOSE 8080

RUN mkdir -p /data && chown -R "$APP_UID:$APP_UID" /data /app
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
ENTRYPOINT ["dotnet", "WorkoutCompanion.Server.dll"]
