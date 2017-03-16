set version=%1
::todo: use FAKE 
dotnet pack prometheus-net/prometheus-net.csproj --configuration Release /p:Version="%version%"