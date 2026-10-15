FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend/ Backend/
COPY Client/Assets/_Game/Core/ Client/Assets/_Game/Core/
RUN dotnet publish Backend/Emberwake.Backend.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 EMBERWAKE_DB=/data/emberwake.db
RUN mkdir /data && chown $APP_UID /data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet","Emberwake.Backend.dll"]
