FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Bu satırı değiştirin - doğru yol ve dosya ismiyle
# Eğer .csproj dosyası alt klasördeyse:
COPY ["PomodoroApi/PomodoroApi.csproj", "PomodoroApi/"]
RUN dotnet restore "PomodoroApi/PomodoroApi.csproj"

# Tüm kaynak kodları kopyala
COPY . .
RUN dotnet build "PomodoroApi/PomodoroApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PomodoroApi/PomodoroApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_HTTP_PORTS=80
EXPOSE 80
ENTRYPOINT ["dotnet", "PomodoroApi.dll"]