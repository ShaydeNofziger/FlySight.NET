using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlySight.Models;
using FlySight.Parsing;

namespace FlySight
{
    /// <summary>
    /// Provides streaming parsers for FlySight CSV logs.
    /// Use <see cref="Read"/> / <see cref="ReadAsync"/> when you already have a <see cref="TextReader"/>,
    /// or use <see cref="ReadFile"/> / <see cref="ReadFileAsync"/> to read directly from a file path.
    /// The reader tolerates blank lines and comment lines (starting with '#').
    /// </summary>
    public static class FlySightReader
    {
        private static readonly string[] DefaultColumns = new[]
        {
            "time","lat","lon","hMSL","velN","velE","velD","hAcc","vAcc","sAcc","gpsFix","numSV"
        };

    /// <summary>
    /// Opens <paramref name="path"/> and returns a lazily-evaluated sequence of parsed <see cref="FlySightSample"/>.
    /// This method streams the file and does not buffer the entire file in memory. The returned sequence is consumed on-demand.
    /// </summary>
    /// <param name="path">Path to a FlySight CSV file.</param>
    /// <param name="cancellationToken">Optional cancellation token used while reading.</param>
    /// <returns>An <see cref="IEnumerable{FlySightSample}"/> yielding parsed samples.</returns>
    /// <exception cref="System.IO.FileNotFoundException">If the file does not exist.</exception>
    public static IEnumerable<FlySightSample> ReadFile(string path, CancellationToken cancellationToken = default)
        {
            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false);
            foreach (var sample in Read(reader, cancellationToken))
            {
                yield return sample;
            }
        }

    /// <summary>
    /// Parses FlySight CSV data from a <see cref="TextReader"/> and yields parsed <see cref="FlySightSample"/> instances.
    /// The parsing is done line-by-line and is tolerant of malformed rows (these are skipped).
    /// </summary>
    /// <param name="reader">A <see cref="TextReader"/> providing CSV data.</param>
    /// <param name="cancellationToken">Optional cancellation token to stop parsing early.</param>
    /// <returns>An <see cref="IEnumerable{FlySightSample}"/> producing samples as they are parsed.</returns>
    public static IEnumerable<FlySightSample> Read(TextReader reader, CancellationToken cancellationToken = default)
        {
            var lineNum = 0;
            string? line;
            string[] columns = Array.Empty<string>();
            bool headerDetected = false;

            while ((line = reader.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNum++;
                if (IsSkippable(line))
                {
                    continue;
                }

                if (!headerDetected)
                {
                    var fields = Csv.SplitLine(line);
                    if (LooksLikeHeader(fields))
                    {
                        columns = fields.Select(f => f.Trim()).ToArray();
                        headerDetected = true;
                        continue; // move to first data line
                    }
                    else
                    {
                        // No header; use default mapping and treat current line as first data row
                        columns = DefaultColumns;
                        headerDetected = true; // proceed to parse data below using current line
                        foreach (var sample in ParseDataLine(fields, columns, lineNum))
                        {
                            yield return sample;
                        }
                        continue;
                    }
                }

                var dataFields = Csv.SplitLine(line);
                foreach (var sample in ParseDataLine(dataFields, columns, lineNum))
                {
                    yield return sample;
                }
            }
        }

    /// <summary>
    /// Asynchronously opens the specified file and yields parsed <see cref="FlySightSample"/> instances.
    /// Use this in async contexts to avoid blocking threads when reading from disk.
    /// </summary>
    /// <param name="path">Path to a FlySight CSV file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of parsed samples.</returns>
    public static async IAsyncEnumerable<FlySightSample> ReadFileAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var stream = File.OpenRead(path);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
            await foreach (var s in ReadAsync(reader, cancellationToken))
            {
                yield return s;
            }
        }

    /// <summary>
    /// Asynchronously parses CSV data supplied by a <see cref="TextReader"/>, yielding <see cref="FlySightSample"/> instances.
    /// This method reads lines with <see cref="TextReader.ReadLineAsync"/> and yields samples as they are parsed.
    /// </summary>
    /// <param name="reader">A <see cref="TextReader"/> to read CSV text from.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{FlySightSample}"/> of parsed samples.</returns>
    public static async IAsyncEnumerable<FlySightSample> ReadAsync(TextReader reader, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var lineNum = 0;
            string? line;
            string[] columns = Array.Empty<string>();
            bool headerDetected = false;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNum++;
                if (IsSkippable(line))
                {
                    continue;
                }

                if (!headerDetected)
                {
                    var fields = Csv.SplitLine(line);
                    if (LooksLikeHeader(fields))
                    {
                        columns = fields.Select(f => f.Trim()).ToArray();
                        headerDetected = true;
                        continue;
                    }
                    else
                    {
                        columns = DefaultColumns;
                        headerDetected = true;
                        foreach (var sample in ParseDataLine(fields, columns, lineNum))
                        {
                            yield return sample;
                        }
                        continue;
                    }
                }

                var dataFields = Csv.SplitLine(line);
                foreach (var sample in ParseDataLine(dataFields, columns, lineNum))
                {
                    yield return sample;
                }
            }
        }

        private static bool IsSkippable(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            var trimmed = line.TrimStart('\uFEFF', ' ', '\t'); // handle BOM
            if (trimmed.Length == 0) return true;
            // Some tools may inject comment lines starting with '#'
            if (trimmed.StartsWith("#")) return true;
            return false;
        }

        private static bool LooksLikeHeader(IReadOnlyList<string> fields)
        {
            if (fields.Count == 0) return false;
            // Consider header if first field is exactly "time" (case-insensitive) and at least 3 standard columns appear
            int known = 0;
            for (int i = 0; i < Math.Min(fields.Count, DefaultColumns.Length); i++)
            {
                var f = fields[i].Trim();
                if (f.Equals(DefaultColumns[i], StringComparison.OrdinalIgnoreCase))
                {
                    known++;
                }
            }
            return known >= 3;
        }

        private static IEnumerable<FlySightSample> ParseDataLine(IReadOnlyList<string> fields, string[] columns, int lineNum)
        {
            // Build raw map when header present; otherwise build a positional map
            var raw = new Dictionary<string, string>(StringComparer.Ordinal);
            var extra = new Dictionary<string, string>(StringComparer.Ordinal);

            int count = fields.Count;
            for (int i = 0; i < count; i++)
            {
                string name = i < columns.Length ? columns[i] : $"col{i + 1}";
                string value = fields[i];
                raw[name] = value;
                if (i >= DefaultColumns.Length)
                {
                    extra[name] = value;
                }
            }

            // Required fields: time, lat, lon, hMSL, velN, velE, velD
            if (!raw.TryGetValue("time", out var tStr))
            {
                yield break; // ignore malformed lines
            }
            if (!raw.TryGetValue("lat", out var latStr)) yield break;
            if (!raw.TryGetValue("lon", out var lonStr)) yield break;
            if (!raw.TryGetValue("hMSL", out var hStr)) yield break;
            if (!raw.TryGetValue("velN", out var vnStr)) yield break;
            if (!raw.TryGetValue("velE", out var veStr)) yield break;
            if (!raw.TryGetValue("velD", out var vdStr)) yield break;

            if (!TryParseTime(tStr, out var time)) yield break;
            if (!Csv.TryParseDouble(latStr, out var lat)) yield break;
            if (!Csv.TryParseDouble(lonStr, out var lon)) yield break;
            if (!Csv.TryParseDouble(hStr, out var hMsl)) yield break;
            if (!Csv.TryParseDouble(vnStr, out var velN)) yield break;
            if (!Csv.TryParseDouble(veStr, out var velE)) yield break;
            if (!Csv.TryParseDouble(vdStr, out var velD)) yield break;

            double? hAcc = null, vAcc = null, sAcc = null;
            int? gpsFix = null, numSv = null;

            if (raw.TryGetValue("hAcc", out var ha) && Csv.TryParseDouble(ha, out var hav)) hAcc = hav;
            if (raw.TryGetValue("vAcc", out var va) && Csv.TryParseDouble(va, out var vav)) vAcc = vav;
            if (raw.TryGetValue("sAcc", out var sa) && Csv.TryParseDouble(sa, out var sav)) sAcc = sav;
            if (raw.TryGetValue("gpsFix", out var gf) && Csv.TryParseInt(gf, out var gfi)) gpsFix = gfi;
            if (raw.TryGetValue("numSV", out var ns) && Csv.TryParseInt(ns, out var nsi)) numSv = nsi;

            yield return new FlySightSample(time, lat, lon, hMsl, velN, velE, velD, hAcc, vAcc, sAcc, gpsFix, numSv, raw, extra);
        }

        private static bool TryParseTime(string s, out DateTimeOffset dto)
        {
            // ISO8601 per spec; assume UTC if "Z" present; otherwise try to parse and normalize to UTC.
            if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
            {
                return true;
            }
            // Some logs may provide fractional seconds with variable precision; try a few common formats
            string[] fmts =
            {
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.fffffffff'Z'",
            };
            foreach (var fmt in fmts)
            {
                if (DateTimeOffset.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
                {
                    return true;
                }
            }
            dto = default;
            return false;
        }
    }
}
