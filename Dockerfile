FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
# .NET 10 discovers Razor files during restore to include the Blazor framework assets.
COPY src/ src/
RUN dotnet restore src/MemByte.Web/MemByte.Web.csproj
RUN dotnet publish src/MemByte.Web/MemByte.Web.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Messaging__AttachmentStorage__LocalRootPath=/home/.messaging-attachments
EXPOSE 8080
RUN mkdir -p /home/.dataprotection-keys /home/.messaging-attachments \
    && chown -R app:app /home
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "MemByte.Web.dll"]
