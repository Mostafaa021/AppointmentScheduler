# API image (plan 014, S-61). Build context = repository root:
#   docker build -f src/AppointmentScheduler.Api/Dockerfile .
# docker-compose.yml builds it as service `api`.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore first, from the project files only, so the package layer is cached until a csproj or the central versions change.
# .editorconfig carries the analyzer rules (e.g. migrations are generated code), so the image builds like a local build.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/AppointmentScheduler.Domain/AppointmentScheduler.Domain.csproj src/AppointmentScheduler.Domain/
COPY src/AppointmentScheduler.Application/AppointmentScheduler.Application.csproj src/AppointmentScheduler.Application/
COPY src/AppointmentScheduler.Infrastructure/AppointmentScheduler.Infrastructure.csproj src/AppointmentScheduler.Infrastructure/
COPY src/AppointmentScheduler.Api/AppointmentScheduler.Api.csproj src/AppointmentScheduler.Api/
RUN dotnet restore src/AppointmentScheduler.Api/AppointmentScheduler.Api.csproj

COPY src/ src/
RUN dotnet publish src/AppointmentScheduler.Api/AppointmentScheduler.Api.csproj -c Release --no-restore -o /app

# Debian-based runtime: includes tzdata (clinic zone Europe/London) and ICU.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AppointmentScheduler.Api.dll"]
