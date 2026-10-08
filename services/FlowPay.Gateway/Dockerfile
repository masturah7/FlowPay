# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY shared/FlowPay.BuildingBlocks/FlowPay.BuildingBlocks.csproj shared/FlowPay.BuildingBlocks/
COPY services/FlowPay.Gateway/FlowPay.Gateway.csproj services/FlowPay.Gateway/
RUN dotnet restore services/FlowPay.Gateway/FlowPay.Gateway.csproj

COPY shared/FlowPay.BuildingBlocks/ shared/FlowPay.BuildingBlocks/
COPY services/FlowPay.Gateway/ services/FlowPay.Gateway/
WORKDIR /src/services/FlowPay.Gateway
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FlowPay.Gateway.dll"]
