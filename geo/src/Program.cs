using System.Diagnostics;
using System.Globalization;
using System.Xml.Serialization;
using geo.models;
using GeoCoordinatePortable;

namespace geo;

internal static class Program
{
    private const double DistanceThresholdMeters = 20.0;

    private static void Main()
    {
        var sw = Stopwatch.StartNew();

        // Deserialize GPX files
        var gpx1 = DeserializeGpx("./data/GPS 1.gpx");
        var gpx2 = DeserializeGpx("./data/GPS 2.gpx");

        // Sort by time (for safety, ensure waypoints are in ascending order)
        gpx1.Waypoints = gpx1.Waypoints.OrderBy(w => w.Time).ToList();
        gpx2.Waypoints = gpx2.Waypoints.OrderBy(w => w.Time).ToList();

        Console.WriteLine("GPX files deserialized and sorted.");

        // Find meeting intervals
        var (didMeet, intervals) = FindMeetingIntervals(gpx1, gpx2, DistanceThresholdMeters);

        // Display results
        if (didMeet)
        {
            Console.WriteLine("They met! Intervals and approximate closest locations:");
            foreach (var (start, end, location, closestWp1, closestWp2) in intervals)
            {
                // Use InvariantCulture + "0.0000" format for consistent '.' decimal
                var latStr = location.Latitude.ToString("0.0000", CultureInfo.InvariantCulture);
                var lonStr = location.Longitude.ToString("0.0000", CultureInfo.InvariantCulture);

                var latWp1 = closestWp1?.Latitude.ToString("0.0000", CultureInfo.InvariantCulture);
                var lonWp1 = closestWp1?.Longitude.ToString("0.0000", CultureInfo.InvariantCulture);

                var latWp2 = closestWp2?.Latitude.ToString("0.0000", CultureInfo.InvariantCulture);
                var lonWp2 = closestWp2?.Longitude.ToString("0.0000", CultureInfo.InvariantCulture);

                Console.WriteLine(
                    $" - From {start:HH:mm:ss} to {end:HH:mm:ss}, " +
                    $"Closest Location: {latStr};{lonStr}, " +
                    $"Closest Wp1: {latWp1};{lonWp1}, " +
                    $"Closest Wp2: {latWp2};{lonWp2}"
                );
            }
        }
        else
        {
            Console.WriteLine("No meeting detected.");
        }

        sw.Stop();
        Console.WriteLine($"Total calculation time: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }

    /// <summary>
    ///     Finds all intervals where the two tracks come within a specified distance threshold.
    /// </summary>
    /// <param name="gpx1">First track data.</param>
    /// <param name="gpx2">Second track data.</param>
    /// <param name="distanceThreshold">Distance threshold (meters).</param>
    /// <returns>
    ///     A tuple containing a boolean indicating if a meeting ever occurred
    ///     and a list of intervals with start/end times and their closest location/waypoints.
    /// </returns>
    private static (bool,
        List<(DateTime Start, DateTime End, GeoCoordinate Location, Waypoint? ClosestWp1, Waypoint?ClosestWp2)>)
        FindMeetingIntervals(Gpx gpx1, Gpx gpx2, double distanceThreshold)
    {
        // Merge and sort all unique times from both GPX tracks
        var allTimes = gpx1.Waypoints.Select(w => w.Time)
            .Concat(gpx2.Waypoints.Select(w => w.Time))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        // We need at least 2 times to form an interval
        var intervals = new List<(DateTime, DateTime, GeoCoordinate, Waypoint?, Waypoint?)>();
        if (allTimes.Count < 2)
            return (false, intervals);

        var inMeeting = false;
        DateTime? meetingStart = null;

        // Track the minimum distance and best location within the current meeting interval
        var currentMinDist = double.MaxValue;
        GeoCoordinate? currentBestLocation = null;

        for (var i = 0; i < allTimes.Count; i++)
        {
            var t = allTimes[i];

            // Interpolate each track at time t
            var loc1 = InterpolateLocation(gpx1.Waypoints, t);
            var loc2 = InterpolateLocation(gpx2.Waypoints, t);

            // If either location cannot be interpolated, skip
            if (loc1 == null || loc2 == null)
            {
                // If we were meeting, close the interval at the previous time
                if (inMeeting && i > 0)
                {
                    intervals.Add(RecordInterval(meetingStart!.Value, allTimes[i - 1],
                        currentBestLocation!, gpx1, gpx2));
                    inMeeting = false;
                    meetingStart = null;
                    currentMinDist = double.MaxValue;
                    currentBestLocation = null;
                }

                continue;
            }

            // Calculate distance
            var dist = loc1.GetDistanceTo(loc2);
            var isMeetingNow = dist <= distanceThreshold;

            switch (inMeeting)
            {
                case false when isMeetingNow:
                    // Just started meeting
                    inMeeting = true;
                    meetingStart = t;
                    currentMinDist = dist;
                    currentBestLocation = new GeoCoordinate(loc1.Latitude, loc1.Longitude);
                    break;
                case true when isMeetingNow:
                {
                    // Still meeting; check if we have a new min distance
                    if (dist < currentMinDist)
                    {
                        currentMinDist = dist;
                        currentBestLocation = new GeoCoordinate(loc1.Latitude, loc1.Longitude);
                    }

                    break;
                }
                case true when !isMeetingNow:
                    // Just ended meeting at the previous time
                    intervals.Add(RecordInterval(meetingStart!.Value, t, currentBestLocation!, gpx1, gpx2));
                    inMeeting = false;
                    meetingStart = null;
                    currentMinDist = double.MaxValue;
                    currentBestLocation = null;
                    break;
            }
        }

        // If we still have an active meeting interval at the end, close it out
        if (inMeeting && meetingStart != null && currentBestLocation != null)
            intervals.Add(RecordInterval(meetingStart.Value, allTimes[^1], currentBestLocation, gpx1, gpx2));

        return (intervals.Count > 0, intervals);
    }

    /// <summary>
    ///     Interpolates location from a list of waypoints using a binary-search approach to find the enclosing segment.
    ///     Returns null if the time is outside the track's range.
    /// </summary>
    /// <param name="waypoints">Ordered list of waypoints (by ascending time).</param>
    /// <param name="t">Time for which to interpolate.</param>
    /// <returns>The interpolated geographic coordinate, or null if out of range.</returns>
    private static GeoCoordinate? InterpolateLocation(List<Waypoint> waypoints, DateTime t)
    {
        if (waypoints.Count == 0)
            return null;

        // If out of range, return null
        if (t < waypoints[0].Time || t > waypoints[^1].Time)
            return null;

        // Binary-search to find two adjacent waypoints around time t
        var left = 0;
        var right = waypoints.Count - 1;

        while (left <= right)
        {
            var mid = (left + right) / 2;
            var wpMidTime = waypoints[mid].Time;

            if (wpMidTime == t)
                // Exact match
                return new GeoCoordinate(waypoints[mid].Latitude, waypoints[mid].Longitude);

            if (wpMidTime < t)
                left = mid + 1;
            else
                right = mid - 1;
        }

        // 'right' is now the index of the waypoint whose time is <= t (if any),
        // 'left' is the index of the waypoint whose time is >= t (if any).
        // We may need to clamp them properly.
        var i1 = Math.Max(0, Math.Min(right, waypoints.Count - 1));
        var i2 = Math.Max(0, Math.Min(left, waypoints.Count - 1));

        var w1 = waypoints[i1];
        var w2 = waypoints[i2];

        // If they ended up the same (e.g., time matches or we are near boundary),
        // just return the coordinate.
        if (w1.Time == w2.Time)
            return new GeoCoordinate(w1.Latitude, w1.Longitude);

        // Otherwise, interpolate
        var totalSecs = (w2.Time - w1.Time).TotalSeconds;
        var frac = (t - w1.Time).TotalSeconds / totalSecs;
        var lat = w1.Latitude + (w2.Latitude - w1.Latitude) * frac;
        var lon = w1.Longitude + (w2.Longitude - w1.Longitude) * frac;
        return new GeoCoordinate(lat, lon);
    }

    /// <summary>
    ///     Deserialize a GPX file into a Gpx object.
    /// </summary>
    /// <param name="filePath">Path of the .gpx file.</param>
    /// <returns>The deserialized Gpx object.</returns>
    private static Gpx DeserializeGpx(string filePath)
    {
        try
        {
            var serializer = new XmlSerializer(typeof(Gpx));
            using var fs = new FileStream(filePath, FileMode.Open);
            return (Gpx)serializer.Deserialize(fs)!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing {filePath}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Records a meeting interval from start to end, resolving the best location's nearest waypoints
    ///     and replacing that best location with the midpoint between those two waypoints.
    /// </summary>
    private static (DateTime, DateTime, GeoCoordinate, Waypoint?, Waypoint?) RecordInterval(
        DateTime start, DateTime end, GeoCoordinate bestLoc, Gpx gpx1, Gpx gpx2)
    {
        // Find closest waypoints in each track
        var closestWp1 = FindClosestWaypoint(gpx1.Waypoints, bestLoc);
        var closestWp2 = FindClosestWaypoint(gpx2.Waypoints, bestLoc);

        if (closestWp1 == null || closestWp2 == null) return (start, end, bestLoc, closestWp1, closestWp2);

        // Force the “closest location” to the midpoint of the two nearest waypoints
        var midLat = (closestWp1.Latitude + closestWp2.Latitude) / 2.0;
        var midLon = (closestWp1.Longitude + closestWp2.Longitude) / 2.0;
        bestLoc = new GeoCoordinate(midLat, midLon);

        return (start, end, bestLoc, closestWp1, closestWp2);
    }

    /// <summary>
    ///     Finds the single closest waypoint to a given location from a list of waypoints.
    /// </summary>
    /// <param name="waypoints">List of waypoints.</param>
    /// <param name="location">Reference location.</param>
    /// <returns>The closest waypoint, or null if there are no waypoints.</returns>
    private static Waypoint? FindClosestWaypoint(List<Waypoint> waypoints, GeoCoordinate location)
    {
        if (waypoints.Count == 0)
            return null;

        return waypoints
            .Select(w => new
            {
                Wp = w,
                Dist = location.GetDistanceTo(new GeoCoordinate(w.Latitude, w.Longitude))
            })
            .OrderBy(x => x.Dist)
            .First().Wp;
    }
}