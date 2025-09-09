using System;
using System.Collections.Generic;
using System.Linq;
using FlySight.Models;

namespace FlySight
{
    /// <summary>
    /// LINQ-style helpers for querying sequences of <see cref="FlySightSample"/>.
    /// These helpers are implemented as deferred-execution enumerable transforms so they compose efficiently with streaming readers.
    /// </summary>
    public static class QueryExtensions
    {
        /// <summary>
        /// Filters the sequence to samples whose <see cref="FlySightSample.Time"/> falls within the specified half-open interval.
        /// </summary>
        /// <param name="source">Source sequence.</param>
        /// <param name="startInclusive">Start time (inclusive) or <c>null</c> for no lower bound.</param>
        /// <param name="endExclusive">End time (exclusive) or <c>null</c> for no upper bound.</param>
        /// <returns>Filtered sequence preserving streaming semantics.</returns>
        public static IEnumerable<FlySightSample> Between(
            this IEnumerable<FlySightSample> source,
            DateTimeOffset? startInclusive,
            DateTimeOffset? endExclusive)
        {
            foreach (var s in source)
            {
                if (startInclusive.HasValue && s.Time < startInclusive.Value) continue;
                if (endExclusive.HasValue && s.Time >= endExclusive.Value) continue;
                yield return s;
            }
        }

        /// <summary>
        /// Filters samples to those with GPS fix value greater than or equal to <paramref name="minFix"/>.
        /// Missing <see cref="FlySightSample.GpsFix"/> values are treated as zero.
        /// </summary>
        public static IEnumerable<FlySightSample> WhereFixAtLeast(
            this IEnumerable<FlySightSample> source,
            int minFix)
        {
            return source.Where(s => (s.GpsFix ?? 0) >= minFix);
        }

        /// <summary>
        /// Filters samples to those with a 3D GPS fix (equivalent to <c>WhereFixAtLeast(3)</c>).
        /// </summary>
        /// <param name="source">Source sequence.</param>
        public static IEnumerable<FlySightSample> WhereFix3D(this IEnumerable<FlySightSample> source)
            => source.WhereFixAtLeast(3);

        /// <summary>
        /// Filters samples to those with horizontal and/or vertical accuracies less than or equal to the specified thresholds.
        /// If an accuracy value is missing for a sample, that sample will be excluded when a corresponding max is supplied.
        /// </summary>
    /// <param name="source">Source sequence.</param>
    /// <param name="maxHorizontalMeters">Maximum horizontal accuracy in meters, or <c>null</c> to ignore.</param>
    /// <param name="maxVerticalMeters">Maximum vertical accuracy in meters, or <c>null</c> to ignore.</param>
        public static IEnumerable<FlySightSample> WithAccuracy(
            this IEnumerable<FlySightSample> source,
            double? maxHorizontalMeters = null,
            double? maxVerticalMeters = null)
        {
            foreach (var s in source)
            {
                if (maxHorizontalMeters.HasValue && (!s.HorizontalAccuracy.HasValue || s.HorizontalAccuracy.Value > maxHorizontalMeters.Value))
                    continue;
                if (maxVerticalMeters.HasValue && (!s.VerticalAccuracy.HasValue || s.VerticalAccuracy.Value > maxVerticalMeters.Value))
                    continue;
                yield return s;
            }
        }

        /// <summary>
        /// Filters samples to those that fall within the given latitude/longitude rectangle (inclusive).
        /// </summary>
        public static IEnumerable<FlySightSample> WithinBounds(
            this IEnumerable<FlySightSample> source,
            double minLat,
            double maxLat,
            double minLon,
            double maxLon)
        {
            foreach (var s in source)
            {
                if (s.Latitude < minLat || s.Latitude > maxLat) continue;
                if (s.Longitude < minLon || s.Longitude > maxLon) continue;
                yield return s;
            }
        }

    /// <summary>
    /// Produces a small summary tuple containing the first sample time, last sample time, and count of samples.
    /// Returns <c>null</c> if the source sequence contains no samples.
    /// </summary>
    /// <returns>A tuple of (start, end, count) or <c>null</c> when empty.</returns>
    public static (DateTimeOffset start, DateTimeOffset end, int count)? Summary(this IEnumerable<FlySightSample> source)
        {
            bool any = false;
            DateTimeOffset min = default;
            DateTimeOffset max = default;
            int count = 0;
            foreach (var s in source)
            {
                if (!any)
                {
                    min = max = s.Time;
                    any = true;
                }
                else
                {
                    if (s.Time < min) min = s.Time;
                    if (s.Time > max) max = s.Time;
                }
                count++;
            }
            return any ? (min, max, count) : null;
        }
    }
}
