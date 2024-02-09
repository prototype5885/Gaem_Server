dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish -p:PublishSingleFile=true -r linux-x64 -c Release --self-contained false -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish -p:PublishSingleFile=true -r osx-x64 -c Release --self-contained false -p:IncludeNativeLibrariesForSelfExtract=true