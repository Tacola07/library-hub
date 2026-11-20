FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY itwebfrontend_mvc/ ./itwebfrontend_mvc/
RUN dotnet publish itwebfrontend_mvc/itwebfrontend.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Tạo thư mục lưu SQLite DB (Render mount volume vào đây)
RUN mkdir -p /app/data

ENV USE_SQLITE=true
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "itwebfrontend.dll"]
