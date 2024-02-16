# Use the .NET SDK image to build the project
FROM mcr.microsoft.com/dotnet/sdk:latest AS build
WORKDIR /src
COPY ["Hartsy.csproj", "./"]
RUN dotnet restore "Hartsy.csproj"
COPY . .
RUN dotnet build "Hartsy.csproj" -c Release -o /app/build

# Publish the project
FROM build AS publish
RUN dotnet publish "Hartsy.csproj" -c Release -o /app/publish

# Build the runtime image
FROM mcr.microsoft.com/dotnet/runtime:latest AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Hartsy.dll"]