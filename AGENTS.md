# Repository Guidelines

## Project Structure & Module Organization

This is a Visual Studio solution for a .NET Framework 4.8 Persona file editor. `PersonaEditor.sln` ties together the WPF desktop app in `PersonaEditor/`, the command-line tool in `PersonaEditorCMD/`, reusable parsing and format logic in `PersonaEditorLib/`, and shared helper projects in `AuxiliaryLibraries/`, `AuxiliaryLibraries.WPF/`, and `AuxiliaryLibraries.WPF.Wrapper/`. UI code is organized under `PersonaEditor/Views`, `ViewModels`, `Controls`, and `Classes`. Runtime assets live in `PersonaEditor/ApplicationResource`, `PersonaEditor/background`, and `PersonaEditor/font`; root images such as `cut_table.jpg` are documentation/reference assets.

## Build, Test, and Development Commands

Use Visual Studio with .NET 10 support or the .NET 10 SDK.

```powershell
dotnet restore PersonaEditor.sln
dotnet build PersonaEditor.sln -c Debug -p:Platform=x64
dotnet build PersonaEditor.sln -c Release -p:Platform=x64
```

`dotnet restore` resolves SDK-style project dependencies. Debug builds are suitable for local development; Release builds produce distributable binaries under each project `bin\x64\Release` folder. Run the desktop app from Visual Studio with `PersonaEditor` as the startup project. Run CLI experiments from `PersonaEditorCMD\bin\x64\Debug\net10.0-windows7.0\PersonaEditorCMD.exe` after building.

## Coding Style & Naming Conventions

The codebase targets .NET 10 with implicit usings and nullable annotations disabled. Keep four-space indentation, braces on their own lines, PascalCase for public types and members, camelCase for locals and parameters, and leading underscores for private fields where already used. Keep WPF views as `.xaml` plus `.xaml.cs` pairs, with matching ViewModels ending in `VM`. Prefer existing helper classes in `AuxiliaryLibraries` and `PersonaEditorLib` before adding new utilities.

## Testing Guidelines

No automated test project is currently present. Before large parser, container, or image-format changes, add or propose focused tests around `PersonaEditorLib` behavior and include small representative fixture files when licensing permits. At minimum, manually verify affected file types through the WPF editor or `PersonaEditorCMD`, and document the exact files or formats checked in the PR.

## Commit & Pull Request Guidelines

Recent history uses short, imperative or descriptive commit subjects such as `Update README.md` and `added file overwrite cli argument`. Keep subjects concise and mention the affected format or UI area when helpful, for example `Add SPR6 texture offset handling`. Pull requests should include a summary, affected formats or screens, manual test notes, linked issues when available, and screenshots for visible WPF changes.

## Security & Configuration Tips

Avoid committing game dumps, proprietary assets, generated binaries, or local `bin/` and `obj/` output. Treat sample files as minimal fixtures and confirm redistribution rights before adding them.
