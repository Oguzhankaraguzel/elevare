# Elevare CMS — build from the REPOSITORY ROOT, not from this folder:
#   docker build -f src/cms/Presentation/Wasm/Wasm/Dockerfile -t elevare-cms .
# The build needs the whole solution because this project references SharedKernel
# and the cms Core/Infrastructure projects.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, with only the project files copied. Docker caches this layer, so
# editing source does not re-download every NuGet package.
# .editorconfig is not optional here: the build sets TreatWarningsAsErrors, and
# that file is where the analyzer rules the codebase deliberately opts out of are
# turned off. Without it the image build fails on rules a local build never sees.
COPY Directory.Build.props Directory.Packages.props Elevare.sln .editorconfig ./
COPY src/SharedKernel/SharedKernel.csproj                                   src/SharedKernel/
COPY src/cms/Core/Domain/Domain.csproj                                      src/cms/Core/Domain/
COPY src/cms/Core/Application/Application.csproj                            src/cms/Core/Application/
COPY src/cms/Infrastructure/Infrastructure/Infrastructure.csproj            src/cms/Infrastructure/Infrastructure/
COPY src/cms/Infrastructure/Persistence/Persistence.csproj                  src/cms/Infrastructure/Persistence/
COPY src/cms/Presentation/Wasm/Wasm/Wasm.csproj                             src/cms/Presentation/Wasm/Wasm/
COPY src/cms/Presentation/Wasm/Wasm.Client/Wasm.Client.csproj               src/cms/Presentation/Wasm/Wasm.Client/
RUN dotnet restore src/cms/Presentation/Wasm/Wasm/Wasm.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/cms/ src/cms/
RUN dotnet publish src/cms/Presentation/Wasm/Wasm/Wasm.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# The runtime image has no HTTP client at all, and the healthcheck below needs one.
# It earns its place twice over: compose uses it to hold the public site back until
# migrations have finished, and any orchestrator can use it to tell "the process
# started" apart from "the app is serving".
# postgresql-client provides pg_dump/pg_restore — DatabaseBackupService shells out
# to them directly, since Postgres (unlike SQL Server) has no in-database BACKUP
# statement of its own.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl postgresql-client \
    && rm -rf /var/lib/apt/lists/*

# Uploads, generated backups and the data-protection keyring must outlive the
# container; compose mounts a volume at each. Created before dropping to the
# non-root user so they are writable.
RUN mkdir -p /app/wwwroot/uploads /app/Backups /app/keys \
    && chown -R $APP_UID:$APP_UID /app/wwwroot/uploads /app/Backups /app/keys

COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080

# /health only answers once the host is running, which is after migrations and
# seeding — so "healthy" here really does mean the schema is in place.
# start-period covers the first run, where the database is created from nothing.
HEALTHCHECK --interval=10s --timeout=5s --start-period=90s --retries=6 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Wasm.dll"]
