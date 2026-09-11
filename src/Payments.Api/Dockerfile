# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Payments.Api/Payments.Api.csproj src/Payments.Api/
COPY src/Contracts/Contracts.csproj src/Contracts/
RUN dotnet restore src/Payments.Api/Payments.Api.csproj

COPY src/Payments.Api/ src/Payments.Api/
COPY src/Contracts/ src/Contracts/
RUN dotnet publish src/Payments.Api/Payments.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Payments.Api.dll"]
