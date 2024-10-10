# copy project file and restore dependencies
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# github pat
ARG GITHUB_TOKEN
ENV GITHUB_TOKEN=${GITHUB_TOKEN}

# copy nuget.config and replace 'myPAT' with actual pat (using sed insead of powershell for image size)
COPY nuget.config .
RUN sed -i "s/myPAT/$GITHUB_TOKEN/g" nuget.config && cat nuget.config

# copy and build project
COPY ["Ratbags.Gateway.csproj", "./"]
RUN dotnet restore "Ratbags.Gateway.csproj" --configfile nuget.config

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

# run!
ENTRYPOINT ["dotnet", "Ratbags.Gateway.dll"]
