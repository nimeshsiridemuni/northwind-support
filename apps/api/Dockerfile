FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./

COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj \
    apps/api/src/SupportDesk.Domain/

COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj \
    apps/api/src/SupportDesk.Application/

COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj \
    apps/api/src/SupportDesk.Infrastructure/

COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    apps/api/src/SupportDesk.Presentation/

RUN dotnet restore \
    apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

COPY apps/api/src/ apps/api/src/

RUN dotnet publish \
    apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release \
    --no-restore \
    --output /out \
    /p:UseAppHost=false

# Supply configuration at runtime, including the database connection and JWT key.
RUN rm -f /out/appsettings*.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /out/ ./

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]