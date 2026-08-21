# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (better layer caching)
COPY omm.csproj .
RUN dotnet restore omm.csproj

# Copy source code and publish
COPY . .
RUN dotnet publish omm.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Expose ports for ASP.NET Core
EXPOSE 8080
EXPOSE 8443

ENTRYPOINT ["dotnet", "omm.dll"]