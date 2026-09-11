# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/BankGateway.Worker/BankGateway.Worker.csproj src/BankGateway.Worker/
COPY src/Contracts/Contracts.csproj src/Contracts/
RUN dotnet restore src/BankGateway.Worker/BankGateway.Worker.csproj

COPY src/BankGateway.Worker/ src/BankGateway.Worker/
COPY src/Contracts/ src/Contracts/
RUN dotnet publish src/BankGateway.Worker/BankGateway.Worker.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "BankGateway.Worker.dll"]
