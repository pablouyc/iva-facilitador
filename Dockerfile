# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./IvaFacilitador.sln ./
COPY ./IvaFacilitador.App/IvaFacilitador.App.csproj ./IvaFacilitador.App/
RUN dotnet restore ./IvaFacilitador.App/IvaFacilitador.App.csproj

COPY . .
RUN dotnet publish ./IvaFacilitador.App/IvaFacilitador.App.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# Render provee la variable PORT; ASPNETCORE_URLS debe escucharla
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
COPY --from=build /app/publish .
# ⚠️ DLL correcto
ENTRYPOINT ["dotnet", "IvaFacilitador.App.dll"]
