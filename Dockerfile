ARG DOTNET_VERSION=7.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./MovieApi.csproj
RUN dotnet publish ./MovieApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000}
EXPOSE 10000

ENTRYPOINT ["dotnet", "MovieApi.dll"]
