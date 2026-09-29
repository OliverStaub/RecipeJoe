FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/RecipeJoe.Api/RecipeJoe.Api.csproj src/RecipeJoe.Api/packages.lock.json src/RecipeJoe.Api/
RUN dotnet restore src/RecipeJoe.Api/RecipeJoe.Api.csproj --locked-mode
COPY src/ src/
# openapi.json is generated at build time locally; not needed (or writable) in the image.
RUN dotnet publish src/RecipeJoe.Api/RecipeJoe.Api.csproj -c Release -o /app --no-restore \
    -p:OpenApiGenerateDocumentsOnBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RecipeJoe.Api.dll"]
