FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ONG.Donation.Worker/ONG.Donation.Worker.csproj", "src/ONG.Donation.Worker/"]
COPY ["src/ONG.Donation.Worker.Domain/ONG.Donation.Worker.Domain.csproj", "src/ONG.Donation.Worker.Domain/"]
COPY ["src/ONG.Donation.Worker.Infrastructure/ONG.Donation.Worker.Infrastructure.csproj", "src/ONG.Donation.Worker.Infrastructure/"]
RUN dotnet restore "./src/ONG.Donation.Worker/ONG.Donation.Worker.csproj"
COPY . .
WORKDIR "/src/src/ONG.Donation.Worker"
RUN dotnet build "./ONG.Donation.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ONG.Donation.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ONG.Donation.Worker.dll"]
