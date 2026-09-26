FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["url-shortener-dotnet.csproj", "./"]
RUN dotnet restore "url-shortener-dotnet.csproj"
COPY . .
RUN dotnet build "url-shortener-dotnet.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "url-shortener-dotnet.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "url-shortener-dotnet.dll"]
