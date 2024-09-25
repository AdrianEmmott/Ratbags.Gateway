# copy project file and restore dependencies
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy and build project
COPY ["Ratbags.Gateway.csproj", "./"]
RUN dotnet restore "Ratbags.Gateway.csproj"

COPY . .
WORKDIR "/src/."
RUN dotnet build "Ratbags.Gateway.csproj" -c Release -o /app/build

# publish
FROM build AS publish
RUN dotnet publish "Ratbags.Gateway.csproj" -c Release -o /app/publish

# run app with .net 8.0 runtime image 
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

# install certs to sim ssl locally
RUN apt-get update && apt-get install -y ca-certificates

# copy published app into image
COPY --from=publish /app/publish .
COPY aspnetapp.pfx /app/aspnetapp.pfx

# run!
ENTRYPOINT ["dotnet", "Ratbags.Gateway.dll"]
