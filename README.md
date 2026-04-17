# FiskalyMock

per buildare

taskkill /F /IM FiskalyMock.exe

dotnet clean -c Release

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true