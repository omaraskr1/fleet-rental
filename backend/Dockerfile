# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first so `dotnet restore` layer-caches independently of
# source changes.
COPY src/FleetRental.Api/FleetRental.Api.csproj src/FleetRental.Api/
COPY src/FleetRental.Application/FleetRental.Application.csproj src/FleetRental.Application/
COPY src/FleetRental.Domain/FleetRental.Domain.csproj src/FleetRental.Domain/
COPY src/FleetRental.Infrastructure/FleetRental.Infrastructure.csproj src/FleetRental.Infrastructure/
RUN dotnet restore src/FleetRental.Api/FleetRental.Api.csproj -r linux-x64

COPY src/ src/
RUN dotnet publish src/FleetRental.Api/FleetRental.Api.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FleetRental.Api.dll"]
