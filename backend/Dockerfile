# Stage 1 — build
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["QueueBacklogIntelligence.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /home/site/wwwroot --no-restore

# Stage 2 — Azure Functions runtime
FROM --platform=linux/amd64 mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS runtime
WORKDIR /home/site/wwwroot
COPY --from=build /home/site/wwwroot .

# Azure Functions required environment variables
ENV AzureWebJobsScriptRoot=/home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true

# Azure Functions host listens on 7071
EXPOSE 7071