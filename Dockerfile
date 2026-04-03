# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ApprovalDemo/api/ApprovalDemo.Api.csproj", "ApprovalDemo/api/"]
RUN dotnet restore "ApprovalDemo/api/ApprovalDemo.Api.csproj"

COPY . .
RUN dotnet build "ApprovalDemo/api/ApprovalDemo.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ApprovalDemo/api/ApprovalDemo.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "ApprovalDemo.Api.dll"]
