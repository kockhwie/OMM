# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (better layer caching)
COPY OMM.Public.csproj .
RUN dotnet restore OMM.Public.csproj

# Copy source code and publish
COPY . .
RUN dotnet publish OMM.Public.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OMM.Public.dll"]