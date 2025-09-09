# Repository Guidelines

## Project Structure & Module Organization
- `src/FlySight/`: Core library targeting `netstandard2.1`.
  - `FlySightReader.cs`: Streaming CSV parser and async reader.
  - `Models/`: Strongly typed row model(s).
  - `Parsing/`: Minimal CSV utilities.
- `tests/FlySight.Tests/`: xUnit tests targeting `net8.0`.
- `FlySight.sln`: Solution file.
- `README.md`: Usage overview.

## Build, Test, and Development Commands
- Build solution: `dotnet build`
- Run tests: `dotnet test -v minimal`
- Pack library: `dotnet pack src/FlySight/FlySight.csproj -c Release -o .\nupkg`
- Optional formatting (if installed): `dotnet format` (no config enforced).

## Coding Style & Naming Conventions
- C# with 4‑space indentation; `Nullable` enabled; `LangVersion` = `latest`.
- Naming: PascalCase for public types/members; camelCase for locals; `_camelCase` for private fields.
- Keep APIs small and streaming‑friendly; avoid loading entire files in memory.
- Use `CultureInfo.InvariantCulture` for numeric/time parsing; prefer explicit access modifiers.

## Testing Guidelines
- Framework: xUnit. Location: `tests/FlySight.Tests`.
- Run: `dotnet test`.
- Name tests as `<UnitUnderTest>_<Condition>_<ExpectedResult>` within `*Tests` classes.
- Include cases for: header and no‑header files, quoted fields, comments/blank lines, async read, extras, and malformed rows.

## Commit & Pull Request Guidelines
- Commits: imperative, concise subject (≤72 chars), e.g., `Add async reader for TextReader`.
- Reference issues in body (`Fixes #123`). Keep changes focused and incremental.
- PRs: clear description, rationale, screenshots/log snippets when helpful. Link related issues. Include tests and update docs when public behavior changes.

## Architecture Overview
- Reader: `FlySightReader` streams lines from `TextReader`/file, auto‑detects header or applies standard column order, and normalizes timestamps to UTC.
- Model: `FlySightSample` exposes typed fields plus `Raw` and `Extra` for future‑proof columns.
- Utilities: lightweight CSV splitter (RFC4180‑style quotes) to avoid heavy dependencies.

## Security & Robustness Tips
- Treat input files as untrusted: ignore malformed rows rather than throwing; validate required columns.
- Preserve unknown columns; avoid breaking changes to public APIs; follow SemVer for releases.
