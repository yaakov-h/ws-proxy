FROM microsoft/aspnetcore-build:2.0 AS build
WORKDIR /src

COPY ws-proxy-server/ws-proxy-server.csproj ./
RUN dotnet restore

COPY ws-proxy-server/ ./
RUN dotnet publish -c Release -o out

FROM microsoft/aspnetcore:2.0
WORKDIR /app
COPY --from=build /src/out .

ENTRYPOINT ["dotnet", "ws-proxy-server.dll"]

EXPOSE 80