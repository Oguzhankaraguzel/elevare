# Elevare public web site — build from the REPOSITORY ROOT, not from this folder:
#   docker build -f src/web/Presentation/WebMvc/Dockerfile -t elevare-web .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# .editorconfig is not optional here: the build sets TreatWarningsAsErrors, and
# that file is where the analyzer rules the codebase deliberately opts out of are
# turned off. Without it the image build fails on rules a local build never sees.
COPY Directory.Build.props Directory.Packages.props Elevare.sln .editorconfig ./
COPY src/SharedKernel/SharedKernel.csproj                          src/SharedKernel/
COPY src/web/Core/Domain/Domain.csproj                             src/web/Core/Domain/
COPY src/web/Core/Application/Application.csproj                   src/web/Core/Application/
COPY src/web/Infrastructure/Infrastructure/Infrastructure.csproj   src/web/Infrastructure/Infrastructure/
COPY src/web/Infrastructure/Persistence/Persistence.csproj         src/web/Infrastructure/Persistence/
COPY src/web/Presentation/WebMvc/WebMvc.csproj                     src/web/Presentation/WebMvc/
RUN dotnet restore src/web/Presentation/WebMvc/WebMvc.csproj

COPY src/SharedKernel/ src/SharedKernel/
COPY src/web/ src/web/
RUN dotnet publish src/web/Presentation/WebMvc/WebMvc.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# See the CMS Dockerfile: the runtime image ships no HTTP client, and a container
# with no healthcheck looks alive to an orchestrator the moment the process starts.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080

# Exempt from maintenance mode on purpose, so closing the site does not make this
# report the container as dead.
HEALTHCHECK --interval=10s --timeout=5s --start-period=40s --retries=6 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "WebMvc.dll"]
