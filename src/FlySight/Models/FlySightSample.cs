using System;
using System.Collections.Generic;

namespace FlySight.Models
{
    /// <summary>
    /// Represents a single parsed sample/row from a FlySight CSV log.
    /// Instances are immutable and intended to be lightweight DTOs for downstream processing.
    /// </summary>
    public sealed class FlySightSample
    {
        /// <summary>Timestamp for this sample, normalized to UTC.</summary>
        public DateTimeOffset Time { get; }
        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; }
        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; }
        /// <summary>Height above mean sea level (meters) as reported in the file.</summary>
        public double HeightMSL { get; }

        /// <summary>Velocity component north (m/s).</summary>
        public double VelocityNorth { get; }
        /// <summary>Velocity component east (m/s).</summary>
        public double VelocityEast { get; }
        /// <summary>Velocity component down (m/s).</summary>
        public double VelocityDown { get; }

        /// <summary>Optional horizontal accuracy in meters, if present.</summary>
        public double? HorizontalAccuracy { get; }
        /// <summary>Optional vertical accuracy in meters, if present.</summary>
        public double? VerticalAccuracy { get; }
        /// <summary>Optional speed accuracy in meters/sec, if present.</summary>
        public double? SpeedAccuracy { get; }
        /// <summary>Optional GPS fix type/quality.</summary>
        public int? GpsFix { get; }
        /// <summary>Optional satellite count (numSV).</summary>
        public int? Satellites { get; }

        /// <summary>
        /// All raw fields read for this row, keyed by header name when available or by positional keys.
        /// This includes both known standard fields and any extra fields present in the file (see <see cref="Extra"/>).
        /// </summary>
        public IReadOnlyDictionary<string, string> Raw { get; }

        /// <summary>
        /// Extra/unrecognized fields that appear to the right of standard columns. Keys are either header names
        /// (when a header row exists) or positional names like <c>col12</c>.
        /// </summary>
        public IReadOnlyDictionary<string, string> Extra { get; }

        /// <summary>Computed 3D speed derived from the three velocity components.</summary>
        public double Speed3D => Math.Sqrt(VelocityNorth * VelocityNorth + VelocityEast * VelocityEast + VelocityDown * VelocityDown);

        internal FlySightSample(
            DateTimeOffset time,
            double lat,
            double lon,
            double hMsl,
            double velN,
            double velE,
            double velD,
            double? hAcc,
            double? vAcc,
            double? sAcc,
            int? gpsFix,
            int? numSv,
            IReadOnlyDictionary<string, string> raw,
            IReadOnlyDictionary<string, string> extra)
        {
            Time = time;
            Latitude = lat;
            Longitude = lon;
            HeightMSL = hMsl;
            VelocityNorth = velN;
            VelocityEast = velE;
            VelocityDown = velD;
            HorizontalAccuracy = hAcc;
            VerticalAccuracy = vAcc;
            SpeedAccuracy = sAcc;
            GpsFix = gpsFix;
            Satellites = numSv;
            Raw = raw;
            Extra = extra;
        }
    }
}

