# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY shared/FlowPay.BuildingBlocks/FlowPay.BuildingBlocks.csproj shared/FlowPay.BuildingBlocks/
COPY services/FlowPay.Notifications/FlowPay.Notifications.csproj services/FlowPay.Notifications/
RUN dotnet restore services/FlowPay.Notifications/FlowPay.Notifications.csproj

COPY shared/FlowPay.BuildingBlocks/ shared/FlowPay.BuildingBlocks/
COPY services/FlowPay.Notifications/ services/FlowPay.Notifications/
WORKDIR /src/services/FlowPay.Notifications
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql probes for Kerberos/GSSAPI support on every new connection; without
# this it logs a (harmless but alarming) ERR-level "cannot open shared object
# file" line and falls back. Installing it keeps that off the logs.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FlowPay.Notifications.dll"]
