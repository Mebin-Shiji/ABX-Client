# Steps to run the application

This is a project for Zicuro Technologies. You need to install .NET 9. 
If not available change the target framework version in ABX-Client.csproj file.

1. Clone the ABX-Client repository
1. Open ..\ABX-Client\ABX-Client folder in any code editor.
3. In the program.cs file, configure your host and port of the node js server. By default host is 'localhost' and port is '3000'.
2. Run the node server.
3. Open terminal on the path ..\ABX-Client\ABX-Client
4. Run command 'dotnet build'
5. Run command 'dotnet run'

The final json will be printed in the console and also at the location ..\ABX-Client\ABX-Client\bin\Debug\net9.0\result.json