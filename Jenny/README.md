# Entitas code generation

This directory contains the Jenny Roslyn CLI and plugins used by the project. The configuration mirrors
the `ecs-survivors` reference and adds its custom single-value component generators.

The generator requires a .NET 6 SDK (a newer SDK may coexist with it).

Before the first generation after adding or removing source files, let Unity regenerate
`src/Hardware Store Simulator/Assembly-CSharp.csproj`. The launchers switch to that project
directory before running Jenny, so generated entries in the `.csproj` stay project-relative
even though the project path contains spaces.

Run on macOS/Linux:

```sh
./Jenny-Gen
```

Run on Windows:

```bat
Jenny-Gen.bat
```

Generated files are written under `Assets/_Project/Code/Generated`. Do not edit them manually.
