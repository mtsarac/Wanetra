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
ARG TARGETARCH
ARG LIBRESPEED_CLI_VERSION=1.0.14
RUN set -eu; \
    case "$TARGETARCH" in \
      amd64) LIBRESPEED_SHA256=89800767ac14085c78a20847ebea23340f6c14a78de0a15c2ac7db8b565c961f ;; \
      arm64) LIBRESPEED_SHA256=75e51a2494d03cb35a92ddbf862b40571a25a1526f3cf3dfa8b1d5d7bc622bd9 ;; \
      *) echo "Unsupported architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    LIBRESPEED_ARCHIVE="librespeed-cli_${LIBRESPEED_CLI_VERSION}_linux_${TARGETARCH}.tar.gz"; \
    curl --fail --silent --show-error --location \
      "https://github.com/librespeed/speedtest-cli/releases/download/v${LIBRESPEED_CLI_VERSION}/${LIBRESPEED_ARCHIVE}" \
      --output /tmp/librespeed-cli.tar.gz; \
    echo "$LIBRESPEED_SHA256  /tmp/librespeed-cli.tar.gz" | sha256sum --check --status; \
    mkdir -p /tmp/librespeed-cli /usr/share/licenses/librespeed-cli; \
    tar -xzf /tmp/librespeed-cli.tar.gz -C /tmp/librespeed-cli; \
    install -m 0755 /tmp/librespeed-cli/librespeed-cli /usr/local/bin/librespeed-cli; \
    install -m 0644 /tmp/librespeed-cli/LICENSE /usr/share/licenses/librespeed-cli/LICENSE; \
    rm -rf /tmp/librespeed-cli /tmp/librespeed-cli.tar.gz
ARG CFSPEEDTEST_VERSION=2.2.2
ARG CFSPEEDTEST_LICENSE_SHA256=508a77d2e7b51d98adeed32648ad124b7b30241a8e70b2e72c99f92d8e5874d1
RUN set -eu; \
    case "$TARGETARCH" in \
      amd64) CFSPEEDTEST_TARGET=x86_64-unknown-linux-gnu; CFSPEEDTEST_SHA256=241df2323e5f7dca5b7e3bbed3061800c00081252738dd10d188439501f69b51 ;; \
      arm64) CFSPEEDTEST_TARGET=aarch64-unknown-linux-gnu; CFSPEEDTEST_SHA256=0d4778e1ca6856dc1f3303337039a15abee21f09a3ef804ea22bfd09d1b41133 ;; \
      *) echo "Unsupported architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac; \
    CFSPEEDTEST_BASE="https://github.com/code-inflation/cfspeedtest"; \
    curl --fail --silent --show-error --location \
      "${CFSPEEDTEST_BASE}/releases/download/v${CFSPEEDTEST_VERSION}/cfspeedtest-${CFSPEEDTEST_TARGET}.tar.gz" \
      --output /tmp/cfspeedtest.tar.gz; \
    echo "$CFSPEEDTEST_SHA256  /tmp/cfspeedtest.tar.gz" | sha256sum --check --status; \
    curl --fail --silent --show-error --location \
      "https://raw.githubusercontent.com/code-inflation/cfspeedtest/v${CFSPEEDTEST_VERSION}/LICENSE.txt" \
      --output /tmp/cfspeedtest-LICENSE; \
    echo "$CFSPEEDTEST_LICENSE_SHA256  /tmp/cfspeedtest-LICENSE" | sha256sum --check --status; \
    mkdir -p /tmp/cfspeedtest /usr/share/licenses/cfspeedtest; \
    tar -xzf /tmp/cfspeedtest.tar.gz -C /tmp/cfspeedtest; \
    install -m 0755 /tmp/cfspeedtest/cfspeedtest /usr/local/bin/cfspeedtest; \
    install -m 0644 /tmp/cfspeedtest-LICENSE /usr/share/licenses/cfspeedtest/LICENSE; \
    rm -rf /tmp/cfspeedtest /tmp/cfspeedtest.tar.gz /tmp/cfspeedtest-LICENSE
COPY --from=backend /app/publish ./
COPY --from=frontend /src/frontend/dist ./wwwroot
RUN mkdir /data && chown -R $APP_UID:$APP_UID /app /data
USER $APP_UID
EXPOSE 8080
VOLUME ["/data"]
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s CMD curl -fsS http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Wanetra.Api.dll"]
