# .NET 6.0 SDK (BepInEx IL2CPP Build Environment)
FROM mcr.microsoft.com/dotnet/sdk:6.0

# Install necessary system packages (git, zip, procps, etc. dependencies)
RUN apt-get update && apt-get install -y git zip unzip procps && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Specify build script execution as the default command
ENTRYPOINT ["/bin/bash", "-c"]
CMD ["dotnet run --project ./build/Build.csproj --target Publish"]
