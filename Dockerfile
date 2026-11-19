# syntax=docker/dockerfile:1.7

FROM ubuntu:26.04 AS native-build

ARG DEBIAN_FRONTEND=noninteractive
RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
      ca-certificates \
      cmake \
      g++ \
      ninja-build \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /source
COPY CMakeLists.txt CMakePresets.json ./
COPY include ./include
COPY src ./src
COPY scenarios ./scenarios

RUN cmake -S . -B /build -G Ninja \
      -DCMAKE_BUILD_TYPE=Release \
      -DAEROGUARD_BUILD_TESTS=OFF \
      -DAEROGUARD_WARNINGS_AS_ERRORS=ON
RUN cmake --build /build --target aeroguard

FROM ubuntu:26.04 AS simulator

ARG DEBIAN_FRONTEND=noninteractive
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates libstdc++6 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=native-build /build/aeroguard /usr/local/bin/aeroguard
COPY scenarios /opt/aeroguard/scenarios

WORKDIR /data
ENTRYPOINT ["aeroguard"]
CMD ["--help"]

FROM simulator AS demo-data

RUN mkdir -p /generated-data
RUN aeroguard \
      --export-all /generated-data \
      --scenarios-dir /opt/aeroguard/scenarios \
      --seed 42

FROM node:22-alpine AS frontend-build

WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
COPY --from=demo-data /generated-data ./public/data
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build

WORKDIR /source
COPY backend/AeroGuard.Backend.sln backend/
COPY backend/src/AeroGuard.Api/AeroGuard.Api.csproj backend/src/AeroGuard.Api/
COPY backend/tests/AeroGuard.Api.Tests/AeroGuard.Api.Tests.csproj backend/tests/AeroGuard.Api.Tests/
RUN dotnet restore backend/src/AeroGuard.Api/AeroGuard.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/src/AeroGuard.Api/AeroGuard.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /publish \
      /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api

ARG DEBIAN_FRONTEND=noninteractive
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=api-build /publish ./
COPY --from=demo-data /generated-data /app/replays

ENV ASPNETCORE_HTTP_PORTS=8080 \
    AEROGUARD_DATA_DIR=/app/replays \
    AEROGUARD_DB_PATH=/data/aeroguard.db

RUN mkdir -p /data && chown "${APP_UID}:${APP_UID}" /data
USER ${APP_UID}

EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=5 \
  CMD curl --fail --silent http://127.0.0.1:8080/api/health >/dev/null || exit 1
ENTRYPOINT ["dotnet", "AeroGuard.Api.dll"]

FROM nginx:1.27-alpine AS dashboard

COPY --from=frontend-build /frontend/dist /usr/share/nginx/html
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --quiet --spider http://127.0.0.1/ || exit 1
