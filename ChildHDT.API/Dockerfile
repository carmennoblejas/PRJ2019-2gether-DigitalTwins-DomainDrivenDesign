#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["ChildHDT.API/ChildHDT.API.csproj", "ChildHDT.API/"]
COPY ["ChildHDT.Infrastructure/ChildHDT.Infrastructure.csproj", "ChildHDT.Infrastructure/"]
COPY ["ChildHDT.Domain/ChildHDT.Domain.csproj", "ChildHDT.Domain/"]
RUN dotnet restore "ChildHDT.API/ChildHDT.API.csproj"
COPY . .
WORKDIR "/src/ChildHDT.API"
RUN dotnet build "ChildHDT.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChildHDT.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ChildHDT.API.dll"]