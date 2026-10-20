# syntax=docker/dockerfile:1

# ---- build: restore and publish the API ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Project files first: restore is cached until a .csproj changes, not on every code edit.
COPY global.json dotnet-tools.json Garbati.StaffIdentityApi.sln ./
COPY src/Garbati.StaffIdentityApi/Garbati.StaffIdentityApi.csproj src/Garbati.StaffIdentityApi/
RUN dotnet restore src/Garbati.StaffIdentityApi/Garbati.StaffIdentityApi.csproj

COPY src/ src/
RUN dotnet publish src/Garbati.StaffIdentityApi/Garbati.StaffIdentityApi.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- bundle: compile the EF Core migrations into one executable ----
FROM build AS bundle
RUN dotnet tool restore \
 && dotnet ef migrations bundle --project src/Garbati.StaffIdentityApi --configuration Release --output /app/efbundle

# ---- migrator image: runs pending migrations, then exits ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
USER $APP_UID
ENTRYPOINT ["sh", "-c", "./efbundle --connection \"$ConnectionStrings__StaffIdentityDb\""]

# ---- final image: the API (default target, so it must be last) ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Garbati.StaffIdentityApi.dll"]
