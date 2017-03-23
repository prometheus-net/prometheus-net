set version=%1
::todo: use FAKE 
::C:\Windows\Microsoft.NET\Framework\v4.0.30319\msbuild prometheus-net.sln /p:Configuration=Release
"C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild" prometheus-net.sln /p:Configuration=Release
nuget pack prometheus-net.nuspec -properties version="%version%"