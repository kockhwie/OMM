# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The Render build context is the repository root so the public project can
# reference OMM.Shared.
COPY OMM.Public/OMM.Public.csproj OMM.Public/
COPY OMM.Shared/OMM.Shared.csproj OMM.Shared/
RUN dotnet restore OMM.Public/OMM.Public.csproj

# Copy source code and publish
COPY OMM.Public OMM.Public
COPY OMM.Shared OMM.Shared
WORKDIR /src/OMM.Public
RUN dotnet publish OMM.Public.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OMM.Public.dll"]
