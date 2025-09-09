using System.Text;
using System.IO;
using System.Linq;
using FlySight;
using FlySight.Models;

namespace FlySight.Tests;

public class ParserTests
{
    [Fact]
    public void Parses_With_Header()
    {
        var csv = string.Join("\n", new[]
        {
            "time,lat,lon,hMSL,velN,velE,velD,hAcc,vAcc,sAcc,gpsFix,numSV",
            "2025-01-01T12:34:56Z,45.0001,-75.0002,1234.5,10.0,0.0,-5.0,1.2,2.3,0.4,3,12"
        });

        using var reader = new StringReader(csv);
        var samples = FlySightReader.Read(reader).ToList();
        Assert.Single(samples);
        var s = samples[0];

        Assert.Equal(new DateTimeOffset(2025,1,1,12,34,56,TimeSpan.Zero), s.Time);
        Assert.Equal(45.0001, s.Latitude, 6);
        Assert.Equal(-75.0002, s.Longitude, 6);
        Assert.Equal(1234.5, s.HeightMSL, 1);
        Assert.Equal(10.0, s.VelocityNorth, 3);
        Assert.Equal(0.0, s.VelocityEast, 3);
        Assert.Equal(-5.0, s.VelocityDown, 3);
        Assert.Equal(3, s.GpsFix);
        Assert.Equal(12, s.Satellites);
        Assert.True(s.Speed3D > 11.18 && s.Speed3D < 11.19); // sqrt(10^2 + 0 + (-5)^2)
        Assert.Empty(s.Extra);
        Assert.Equal("1234.5", s.Raw["hMSL"]);
    }

        [Fact]
        public void Parses_SampleData_From_File()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_data.txt");
            Assert.True(File.Exists(path), $"Sample data file not found: {path}");
            var csv = File.ReadAllText(path);
            using var reader = new StringReader(csv);
            var samples = FlySightReader.Read(reader).ToList();
        Assert.Equal(3, samples.Count);
        AssertBasicSample(samples[0], new DateTimeOffset(2025,9,9,12,0,0,TimeSpan.Zero), 37.7749, -122.4194, 100);
        }

        [Fact]
        public async Task Parses_SampleData_From_File_Async()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sample_data.txt");
            Assert.True(File.Exists(path), $"Sample data file not found: {path}");

            var list = new List<FlySightSample>();
            using var fs = File.OpenRead(path);
            using var sr = new StreamReader(fs);
            await foreach (var s in FlySightReader.ReadAsync(sr))
            {
                list.Add(s);
            }

            Assert.Equal(3, list.Count);
            AssertBasicSample(list[0], new DateTimeOffset(2025,9,9,12,0,0,TimeSpan.Zero), 37.7749, -122.4194, 100);
        }

        [Fact]
        public async Task Reads_Empty_File_Returns_No_Samples_Async()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                // Ensure file is empty
                File.WriteAllText(tmp, string.Empty);
                var results = new List<FlySightSample>();
                using var fs = File.OpenRead(tmp);
                using var sr = new StreamReader(fs);
                await foreach (var s in FlySightReader.ReadAsync(sr)) results.Add(s);
                Assert.Empty(results);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void Skips_Malformed_Rows_File()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                // Write a header and a malformed row (missing columns)
                File.WriteAllText(tmp, "time,lat,lon\n2025-01-01T00:00:00Z,1\n");
                using var sr = new StreamReader(tmp);
                var list = FlySightReader.Read(sr).ToList();
                Assert.Empty(list);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public async Task Large_File_Streaming_Async()
        {
            var tmp = Path.GetTempFileName();
            const int N = 5000;
            try
            {
                using (var sw = new StreamWriter(tmp, false, Encoding.UTF8))
                {
                    sw.WriteLine("time,lat,lon,hMSL,velN,velE,velD,hAcc,vAcc,sAcc,gpsFix,numSV");
                    var t0 = DateTimeOffset.Parse("2025-01-01T00:00:00Z");
                    for (int i = 0; i < N; i++)
                    {
                        sw.WriteLine($"{t0.AddSeconds(i):o},0,0,0,0,0,0,1,1,1,3,7");
                    }
                }

                var count = 0;
                using var fs2 = File.OpenRead(tmp);
                using var sr2 = new StreamReader(fs2);
                await foreach (var s in FlySightReader.ReadAsync(sr2))
                {
                    count++;
                }
                Assert.Equal(N, count);
            }
            finally { File.Delete(tmp); }
        }

        private static void AssertBasicSample(FlySightSample s, DateTimeOffset time, double lat, double lon, double height)
        {
            Assert.Equal(time, s.Time);
            Assert.Equal(lat, s.Latitude, 4);
            Assert.Equal(lon, s.Longitude, 4);
            Assert.Equal(height, s.HeightMSL);
        }

    [Fact]
    public void Parses_Without_Header_Assumes_Default_Order()
    {
        var csv = "2025-01-01T12:34:56Z,45.1,-75.2,1000,1,2,3,4,5,0.6,2,9";
        using var reader = new StringReader(csv);
        var samples = FlySightReader.Read(reader).ToList();
        Assert.Single(samples);
        var s = samples[0];
        Assert.Equal(45.1, s.Latitude, 3);
        Assert.Equal(-75.2, s.Longitude, 3);
        Assert.Equal(1, s.VelocityNorth, 3);
        Assert.Equal(2, s.VelocityEast, 3);
        Assert.Equal(3, s.VelocityDown, 3);
        Assert.Equal(2, s.GpsFix);
        Assert.Equal(9, s.Satellites);
    }

    [Fact]
    public void Skips_Blank_And_Comment_Lines()
    {
        var csv = string.Join("\n", new[]
        {
            "# FlySight log",
            "",
            "time,lat,lon,hMSL,velN,velE,velD,hAcc,vAcc,sAcc,gpsFix,numSV",
            "2025-01-01T00:00:00Z,0,0,0,0,0,0,,,,3,8"
        });
        using var reader = new StringReader(csv);
        var samples = FlySightReader.Read(reader).ToList();
        Assert.Single(samples);
        Assert.Equal(3, samples[0].GpsFix);
        Assert.Equal(8, samples[0].Satellites);
    }

    [Fact]
    public void Preserves_Extra_Columns()
    {
        var csv = string.Join("\n", new[]
        {
            "time,lat,lon,hMSL,velN,velE,velD,hAcc,vAcc,sAcc,gpsFix,numSV,extra1,extra2",
            "2025-01-01T00:00:00Z,1,2,3,4,5,6,7,8,9,3,10,foo,bar"
        });
        using var reader = new StringReader(csv);
        var s = FlySightReader.Read(reader).Single();
        Assert.Equal("foo", s.Raw["extra1"]);
        Assert.Equal("bar", s.Raw["extra2"]);
        Assert.Equal(2, s.Extra.Count);
        Assert.True(s.Extra.ContainsKey("extra1"));
        Assert.True(s.Extra.ContainsKey("extra2"));
    }

    [Fact]
    public void Parses_Quoted_Extra_Field()
    {
        var csv = string.Join("\n", new[]
        {
            "time,lat,lon,hMSL,velN,velE,velD,hAcc,vAcc,sAcc,gpsFix,numSV,note",
            "2025-01-01T00:00:00Z,1,2,3,4,5,6,7,8,9,3,10,\"hello, world\""
        });
        using var reader = new StringReader(csv);
        var s = FlySightReader.Read(reader).Single();
        Assert.Equal("hello, world", s.Raw["note"]);
        Assert.True(s.Extra.ContainsKey("note"));
    }

    [Fact]
    public void Skips_Malformed_Lines()
    {
        var csv = string.Join("\n", new[]
        {
            "time,lat,lon",
            "2025-01-01T00:00:00Z,1,2" // missing required columns
        });
        using var reader = new StringReader(csv);
        var list = FlySightReader.Read(reader).ToList();
        Assert.Empty(list);
    }

    [Fact]
    public async Task Reads_Async()
    {
        var csv = string.Join("\n", new[]
        {
            "time,lat,lon,hMSL,velN,velE,velD,hAcc,vAcc,sAcc,gpsFix,numSV",
            "2025-01-01T00:00:00Z,0,0,0,1,2,3,,,,3,7",
            "2025-01-01T00:00:01Z,0,0,0,1,2,3,,,,3,7"
        });
        using var reader = new StringReader(csv);
        var list = new List<FlySightSample>();
        await foreach (var s in FlySightReader.ReadAsync(reader))
        {
            list.Add(s);
        }
        Assert.Equal(2, list.Count);
        Assert.All(list, s => Assert.Equal(3, s.GpsFix));
    }

    [Fact]
    public void Query_Helpers_Work()
    {
        var csv = string.Join("\n", new[]
        {
            "time,lat,lon,hMSL,velN,velE,velD,hAcc,vAcc,sAcc,gpsFix,numSV",
            "2025-01-01T00:00:00Z,0,0,0,1,2,3,3.0,3.0,0.5,3,7",
            "2025-01-01T00:00:01Z,0,0,0,1,2,3,10.0,3.0,0.5,2,7",
            "2025-01-01T00:00:02Z,0,0,0,1,2,3,2.0,10.0,0.5,4,7"
        });
        using var reader = new StringReader(csv);
        var items = FlySightReader.Read(reader).ToList();

        var filtered = items
            .WhereFix3D()
            .WithAccuracy(maxHorizontalMeters: 5.0, maxVerticalMeters: 5.0)
            .Between(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), DateTimeOffset.Parse("2025-01-01T00:00:02Z"))
            .ToList();

        // Only first row matches: fix >= 3, both accuracies <= 5, and in [t0, t2)
        Assert.Single(filtered);

        var summary = items.Summary();
        Assert.NotNull(summary);
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), summary.Value.start);
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:02Z"), summary.Value.end);
        Assert.Equal(3, summary.Value.count);
    }

    [Fact]
    public void Parses_Timestamps_With_Offset_And_Fraction()
    {
        var csv = string.Join("\n", new[]
        {
            "time,lat,lon,hMSL,velN,velE,velD",
            "2025-01-01T12:34:56.123Z,0,0,0,0,0,0"
        });
        using var r1 = new StringReader(csv);
        var s1 = FlySightReader.Read(r1).Single();
        Assert.Equal(123, s1.Time.Millisecond);
        Assert.Equal(TimeSpan.Zero, s1.Time.Offset);

        var csv2 = string.Join("\n", new[]
        {
            "time,lat,lon,hMSL,velN,velE,velD",
            "2025-01-01T12:34:56+02:00,0,0,0,0,0,0"
        });
        using var r2 = new StringReader(csv2);
        var s2 = FlySightReader.Read(r2).Single();
        Assert.Equal(TimeSpan.Zero, s2.Time.Offset); // normalized to UTC
        Assert.Equal(new DateTimeOffset(2025,1,1,10,34,56,TimeSpan.Zero), s2.Time);
    }
}
