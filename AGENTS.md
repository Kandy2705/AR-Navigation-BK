# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity project (`TestARMultiSet.sln`) targeting Unity `6000.0.44f1`.

- `Assets/Code`: AR navigation/gameplay scripts (core runtime logic).
- `Assets/UI/Scripts`: UI Toolkit controllers, services, routing, and screen logic.
- `Assets/Scenes`, `Assets/Scene`: Unity scenes and scene-specific assets.
- `Assets/Plugins`, `Assets/Samples`, `Assets/RestClient`: third-party SDKs and vendor content.
- `Packages/manifest.json`: Unity package dependencies (includes `com.unity.test-framework`).
- `ProjectSettings/`: editor/build configuration tracked in source control.

Do not edit generated folders (`Library/`, `Temp/`, `Logs/`) or commit their contents.

## Build, Test, and Development Commands
Use Unity Editor for day-to-day work, or run headless commands from repo root:

```powershell
# EditMode tests
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode-results.xml -quit

# PlayMode tests
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode-results.xml -quit
```

Open the project with Unity Hub using editor version `6000.0.44f1`.

## Coding Style & Naming Conventions
- Language: C#.
- Indentation: 4 spaces, UTF-8 text files.
- Types/methods/properties: `PascalCase` (`NavigationTarget`, `HandleSubmit`).
- Local variables/private fields: `camelCase` (`inputField`, `keyWords`).
- One MonoBehaviour per file; filename must match class name.
- Keep UI controllers in `Assets/UI/Scripts/Controller`, service logic in `Assets/UI/Scripts/Service`.

## Testing Guidelines
Unity Test Framework is available, but test coverage is currently sparse. New features should add tests:

- EditMode tests: `Assets/Tests/EditMode/`
- PlayMode tests: `Assets/Tests/PlayMode/`
- File pattern: `FeatureNameTests.cs`
- Test method pattern: `Method_WhenCondition_ExpectedResult`

Prioritize navigation logic, API service parsing, and UI routing behavior.

## Commit & Pull Request Guidelines
Recent history shows short, task-focused commits and branch names like `khanh-*`, `UI-remake`. Follow that style with clearer scope:

- Commit format: `<area>: <action>` (example: `ui-login: fix remember-me state restore`).
- Keep commits atomic (code + related scene/prefab/meta updates together).
- PRs should include: summary, changed scenes/prefabs, test evidence (logs or screenshots), and linked issue/task.
- For UI changes, attach before/after screenshots or short recordings.
