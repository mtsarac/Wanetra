FROM oven/bun:1 AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/bun.lock ./
RUN bun install --frozen-lockfile
COPY frontend/ ./
RUN bun run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/ ./backend/
RUN dotnet restore backend/Wanetra.Api/Wanetra.Api.csproj
RUN dotnet publish backend/Wanetra.Api/Wanetra.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    WANETRA_DATA_PATH=/data \
    WANETRA_PORT=8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=backend /app/publish ./
COPY --from=frontend /src/frontend/dist ./wwwroot
RUN mkdir /data && chown -R $APP_UID:$APP_UID /app /data
USER $APP_UID
EXPOSE 8080
VOLUME ["/data"]
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s CMD curl -fsS http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Wanetra.Api.dll"]
