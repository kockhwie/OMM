# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The Render build context is the repository root so the Admin project can
# reference OMM.Shared.
COPY OMM.Admin/OMM.Admin.csproj OMM.Admin/
COPY OMM.Shared/OMM.Shared.csproj OMM.Shared/
RUN dotnet restore OMM.Admin/OMM.Admin.csproj

# Copy source code and publish
COPY OMM.Admin OMM.Admin
COPY OMM.Shared OMM.Shared
WORKDIR /src/OMM.Admin
RUN dotnet publish OMM.Admin.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "OMM.Admin.dll"]
