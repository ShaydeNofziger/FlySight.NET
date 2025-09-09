FlySight.NET
============

FlySight.NET is a small, focused .NET library for parsing FlySight CSV logs (including FlySight 2). It provides memory-efficient streaming readers (sync + async), a lightweight sample model, and LINQ-style helpers for filtering and summarizing data.

This README documents what the library does, its design principles, and practical examples to get you productive quickly.

## Key features

- Streaming parsing from any TextReader (works with files, network streams, pipes).
- Synchronous and asynchronous APIs: read line-by-line without loading entire files into memory.
- Robust handling of headers, missing/extra columns, comments, and blank lines.
- Preserves unknown/extra columns via `Raw` and `Extra` maps on each sample.
- Small, dependency-free API surface targeting .NET Standard 2.1.

## Installation

- Target framework: `netstandard2.1` (works from .NET Core/.NET 5+/Mono that supports netstandard2.1).
- Add a project reference to the `FlySight` project, or build and reference the assembly. If packaged, install via NuGet when available.

Example (add project reference inside solution):

```pwsh
# from repository root
dotnet add tests/FlySight.Tests/FlySight.Tests.csproj reference src/FlySight/FlySight.csproj
```

## Quick start

Below are minimal examples showing common usage patterns. Replace `path/to/log.csv` with your file path.

### Synchronous file streaming

```csharp
using FlySight;

foreach (var sample in FlySightReader.ReadFile("path/to/log.csv"))
{
        Console.WriteLine($"{sample.Time:u} lat={sample.Latitude:F6} lon={sample.Longitude:F6} 3d={sample.Speed3D:F2} m/s");
}
```

### Asynchronous streaming (useful for large files or UI apps)

```csharp
using FlySight;

await foreach (var sample in FlySightReader.ReadFileAsync("path/to/log.csv"))
{
        // process sample
}
```

### Parsing from an arbitrary TextReader / stream

Use `Read` and `ReadAsync` when you already have a `TextReader` (e.g., a `StreamReader` over a network stream or a compressed input).

```csharp
using var stream = File.OpenRead("path/to/log.csv");
using var reader = new StreamReader(stream);

foreach (var sample in FlySightReader.Read(reader))
{
        // ...
}

// async variant
using var stream2 = File.OpenRead("path/to/log.csv");
using var reader2 = new StreamReader(stream2);
await foreach (var sample in FlySightReader.ReadAsync(reader2))
{
        // ...
}
```

## API reference (essential surface)

- FlySightReader
    - IEnumerable<FlySightSample> Read(TextReader reader, CancellationToken cancellationToken = default)
    - IAsyncEnumerable<FlySightSample> ReadAsync(TextReader reader, CancellationToken cancellationToken = default)
    - IEnumerable<FlySightSample> ReadFile(string path, CancellationToken cancellationToken = default)
    - IAsyncEnumerable<FlySightSample> ReadFileAsync(string path, CancellationToken cancellationToken = default)

- FlySightSample (model)
    - DateTimeOffset Time
    - double Latitude
    - double Longitude
    - double HeightMSL
    - double VelocityNorth
    - double VelocityEast
    - double VelocityDown
    - double? HorizontalAccuracy
    - double? VerticalAccuracy
    - double? SpeedAccuracy
    - int? GpsFix
    - int? Satellites
    - IReadOnlyDictionary<string,string> Raw — all raw fields by name (header names preserved)
    - IReadOnlyDictionary<string,string> Extra — unknown/right-hand fields preserved
    - double Speed3D — computed 3D speed

- QueryExtensions (LINQ helpers)
    - Between(startInclusive, endExclusive)
    - WhereFixAtLeast(minFix) / WhereFix3D()
    - WithAccuracy(maxHorizontalMeters, maxVerticalMeters)
    - WithinBounds(minLat, maxLat, minLon, maxLon)
    - Summary() — returns (start, end, count)? or null when empty

## File format and parsing rules

- Supported input: standard FlySight CSV logs. The library accepts logs with or without a header row.
- If the first non-empty, non-comment line looks like a header (the parser checks for known column names like `time`, `lat`, `lon` in the first few fields), that header is used to name fields in `Raw`/`Extra` maps. Otherwise, a default column order is assumed:

    time, lat, lon, hMSL, velN, velE, velD, hAcc, vAcc, sAcc, gpsFix, numSV

- Blank lines and lines starting with `#` are ignored.
- If required fields are missing on a data row (time, lat, lon, hMSL, velN, velE, velD), the row is skipped.
- Time strings are parsed as ISO8601 with normalization to UTC. Fractional-second formats are supported.
- Extra columns to the right are kept and accessible through `FlySightSample.Extra` and `FlySightSample.Raw`.

## Error handling and resilience

- Parsing is defensive: malformed rows are ignored rather than raising exceptions — this is intentional for streaming robustness.
- Callers who need strict validation should post-filter and validate `FlySightSample.Raw` values or wrap parsing calls and detect when expected rows are missing.
- Cancellation tokens are honored by the `ReadFile` and `ReadFileAsync` APIs where provided.

## Performance notes

- The parser is intentionally streaming and avoids buffering whole files in memory. Use the async readers for large files or IO-bound scenarios.
- For very large workloads, prefer `ReadAsync` with `await foreach` to avoid blocking threadpool threads.
- Buffer sizes and encoding detection mimic standard `StreamReader` defaults and can be adjusted by creating your own `StreamReader` and calling `Read`/`ReadAsync` directly.

## Testing

- Unit tests live under `tests/FlySight.Tests`. Run them with:

```pwsh
dotnet test "c:\Repos\FlySight.NET\FlySight.NET.sln"
```

- The test suite includes parsing, async streaming, malformed lines handling, and large-file streaming tests.

## Examples and common patterns

- Filtering GPS-quality points and summarizing:

```csharp
var good = FlySightReader.ReadFile("log.csv")
        .WhereFix3D()
        .WithAccuracy(maxHorizontalMeters: 5.0, maxVerticalMeters: 5.0)
        .Between(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), DateTimeOffset.Parse("2025-01-02T00:00:00Z"))
        .ToList();

var summary = good.Summary();
```

- Accessing extra fields (header present):

```csharp
var sample = FlySightReader.ReadFile("log.csv").First();
if (sample.Raw.TryGetValue("note", out var note)) Console.WriteLine(note);
foreach (var kv in sample.Extra) Console.WriteLine($"{kv.Key} = {kv.Value}");
```

## Contributing

- The repository is organized with the `src/FlySight` project and `tests/FlySight.Tests`.
- Please open issues for bugs or feature requests. For code changes, fork, create a feature branch, add tests for new behavior, and open a pull request.

Guidelines
 - Keep public API surface small and stable.
 - Add tests for parsing edge cases and streaming behavior.
 - Keep performance characteristics in mind (streaming vs in-memory).

## Troubleshooting

- If you see zero samples parsed, confirm the file encoding and that the first non-comment line is a header or valid data row.
- If using the library in a non-UTF8 environment, create the `StreamReader` yourself with the correct encoding and call `Read`/`ReadAsync`.
- To debug parsing, inspect `FlySightSample.Raw` to see exactly what column values were read.

## License

This project follows the license present in the repository (add or update the LICENSE file as needed).

## Contact / Maintainers

- See `AGENTS.md` or the repository maintainers list for contact and contribution routing.
