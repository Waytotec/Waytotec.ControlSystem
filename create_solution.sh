dotnet new sln -n Waytotec.ControlSystem
dotnet new wpf -n Waytotec.ControlSystem.App -o Waytotec.ControlSystem/Waytotec.ControlSystem.App
dotnet sln Waytotec.ControlSystem.sln add Waytotec.ControlSystem/Waytotec.ControlSystem.App/Waytotec.ControlSystem.App.csproj
dotnet new classlib -n Waytotec.ControlSystem.Core -o Waytotec.ControlSystem/Waytotec.ControlSystem.Core
dotnet sln Waytotec.ControlSystem.sln add Waytotec.ControlSystem/Waytotec.ControlSystem.Core/Waytotec.ControlSystem.Core.csproj
dotnet new classlib -n Waytotec.ControlSystem.Infrastructure -o Waytotec.ControlSystem/Waytotec.ControlSystem.Infrastructure
dotnet sln Waytotec.ControlSystem.sln add Waytotec.ControlSystem/Waytotec.ControlSystem.Infrastructure/Waytotec.ControlSystem.Infrastructure.csproj
dotnet new classlib -n Waytotec.ControlSystem.IoC -o Waytotec.ControlSystem/Waytotec.ControlSystem.IoC
dotnet sln Waytotec.ControlSystem.sln add Waytotec.ControlSystem/Waytotec.ControlSystem.IoC/Waytotec.ControlSystem.IoC.csproj
dotnet new classlib -n Waytotec.ControlSystem.Tests -o Waytotec.ControlSystem/Waytotec.ControlSystem.Tests
dotnet sln Waytotec.ControlSystem.sln add Waytotec.ControlSystem/Waytotec.ControlSystem.Tests/Waytotec.ControlSystem.Tests.csproj
