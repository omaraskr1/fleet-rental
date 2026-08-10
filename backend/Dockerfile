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

# Microsoft.ML.Mkl.Redist (pulled in transitively by Microsoft.ML.TimeSeries,
# for the revenue forecast's SSA model) bundles Intel's OpenMP runtime for
# Windows but not for Linux — it assumes the OS already provides libiomp5.so
# under that exact name. The base aspnet image doesn't, so the revenue
# forecast and category demand endpoints 500 with DllNotFoundException
# without this. Same fix as .github/workflows' "Install MKL's OpenMP runtime
# dependency" CI step — see that file's comment for why libomp5 (not GNU's
# libgomp) is the correct substitute.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libomp5 \
    && ln -sf "$(find /usr/lib -name 'libomp.so.*' | head -n1)" /usr/lib/x86_64-linux-gnu/libiomp5.so \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FleetRental.Api.dll"]
