FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore SpanishPractice.sln
RUN dotnet publish src/SpanishPractice.Api/SpanishPractice.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
RUN mkdir -p /app/data /app/wwwroot/uploads
EXPOSE 8080
ENTRYPOINT ["dotnet", "SpanishPractice.Api.dll"]
