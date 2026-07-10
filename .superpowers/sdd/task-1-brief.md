### Task 1: Centralize Project Configuration

**Files:**
- Create: `Directory.Build.props`
- Modify: `SimpleFramework.csproj`
- Modify: `Collections/Collections.csproj`
- Modify: `ECS/ECS.csproj`
- Modify: `GDExt/GDExt.csproj`
- Modify: `Maths/Maths.csproj`
- Modify: `Net/Net.csproj`
- Modify: `Patterns/Patterns.csproj`
- Modify: `Test/Test.csproj`
- Modify: `Toolkit/Toolkit.csproj`
- Modify: `Utility/Utility.csproj`

**Interfaces:**
- Produces: every project inherits `TargetFramework=net8.0`, `ImplicitUsings=enable`, `Nullable=enable`, and shared Debug/Release output paths from `Directory.Build.props`.

- [x] **Step 1: Add shared MSBuild props**

Create `Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <OutputPath>$(MSBuildThisFileDirectory)bin\Debug\</OutputPath>
  </PropertyGroup>

  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <OutputPath>$(MSBuildThisFileDirectory)bin\Release\</OutputPath>
  </PropertyGroup>
</Project>
```

- [x] **Step 2: Remove duplicated shared properties**

In each project file, remove `TargetFramework`, `ImplicitUsings`, `Nullable`, and repeated Debug/Release `OutputPath` groups. Keep project-specific values such as `RootNamespace`, `AssemblyName`, `ProjectReference`, compile removes, and conditional package references.

- [x] **Step 3: Verify expected nullable/build red state**

Run:

```powershell
dotnet build .\SimpleFramework.sln
```

Expected: build may fail or warn because the root framework project now participates in nullable analysis. Record the first warning/error categories for Task 2.

- [x] **Step 4: Commit**

```powershell
git add Directory.Build.props *.csproj Collections\*.csproj ECS\*.csproj GDExt\*.csproj Maths\*.csproj Net\*.csproj Patterns\*.csproj Test\*.csproj Toolkit\*.csproj Utility\*.csproj
git commit -m "build: centralize project defaults"
```
