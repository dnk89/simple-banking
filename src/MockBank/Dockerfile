# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/MockBank/MockBank.csproj src/MockBank/
COPY src/Contracts/Contracts.csproj src/Contracts/
RUN dotnet restore src/MockBank/MockBank.csproj

COPY src/MockBank/ src/MockBank/
COPY src/Contracts/ src/Contracts/
RUN dotnet publish src/MockBank/MockBank.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MockBank.dll"]
