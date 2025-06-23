## OverView

To run this console app you need to install .NET 9. 
.NET 8 is also supported but you'll need to change the target framework version in ABX-Client.csproj file to net8.0.

## Steps to run the application

1. Clone the ABX-Client repository
2. Open ..\ABX-Client\ABX-Client folder in any code editor.
3. In the program.cs file, configure your host and port of the node js server. By default host is 'localhost' and port is '3000'.
4. Run the node server.
5. If you have visual studio, just open the .sln file and run the .NET app from there. Otherwise
6. Open terminal on the path ..\ABX-Client\ABX-Client
7. Run command 'dotnet run'


The final json will be printed in the console and also at the location ..\ABX-Client\ABX-Client\bin\Debug\net9.0\result.json
