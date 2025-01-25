# GEO (CTF)

## Challenge Description

> **We have confiscated the phones of two suspects. We believe they met in person. Can you pinpoint exactly where?**

## Overview

In this challenge, you are provided with two GPX files containing geolocation data from each suspect. Your task is to
analyze these datasets and determine the specific location of their meeting.

## Methodology

Several potential approaches can be used to identify the intersection point:

### 1. Visual Inspection

A straightforward method is to visualize both GPX tracks on a map (e.g., with **Google Earth Pro** or a GIS tool). By
overlaying the two paths and using a time slider to align timestamps, you can visually detect where the suspects’ routes
intersect.

- **Advantages:** Simple and intuitive for small datasets (fewer than 200 points here).
- **Drawbacks:** Labor-intensive and prone to error with large datasets or many suspects.

### 2. Automated Analysis with Code

For a more scalable solution, you can write a program (in **C#** or any other language) to:

1. Parse and deserialize the GPX files into timestamped coordinates.
2. Interpolate between recorded points to fill in gaps where the phones were not actively tracking.
3. Identify spatial and temporal overlaps—locations where both suspects are close at the same time.
4. Output the location and time of the intersection.

- **Advantages:** Highly efficient for large or multiple datasets; reusable.
- **Challenges:** Requires careful handling of data gaps (interpolation) and time matching to avoid false positives.

### Why Interpolation Matters

When visualizing in tools like Google Earth Pro, lines are automatically drawn between waypoints. Re-creating this
programmatically involves calculating paths between recorded points—often requiring interpolation to accurately
determine whether two individuals occupied the same place at the same time.

## GIS Solution

For a quick win, we load both GPX files into [Google Earth Pro](https://www.google.com/earth/about/versions/) and
color-code each suspect’s route. Adjusting the time slider reveals where and when they intersect.

- **Suspect 1** (`GPS 1.gpx`): **Blue Path**
- **Suspect 2** (`GPS 2.gpx`): **Red Path**

### Visualizing the Paths

![Visualized Routes](./images/Visualised%20Routes.png)

### Visual Inspection Steps

1. **Overlay** the paths.
2. **Synchronize** times using the time slider.
3. **Pinpoint** the meeting location and note the timestamps.

### Intersection Details (UTC)

- **Suspect 2** departs Entrepotdok 4, 1018 AD Amsterdam at **9:54 AM**, walks around Oosterpark, and returns to
  Entrepotdok by **11:02 AM**.
- **Suspect 1** leaves Westerstraat, 1015 LT Amsterdam at **12:32 PM**, heads to Central Station, then to Café de Jaren,
  arriving at **1:21 PM**.
- **Suspect 2** moves toward Central Station and briefly retraces Suspect 1’s path but makes a sudden turn.
- At the exact moment Suspect 2 turns around, Suspect 1 leaves Café de Jaren. They both proceed toward the Holland
  Casino area, then parallel each other’s routes into Vondelpark from different entrances.
- **Suspect 1** enters the meeting location at **4:54 PM**; **Suspect 2** arrives via another entrance at **5:05 PM**.
- They officially meet at **5:11 PM** at the same coordinates.

#### Waypoints of Interest

- **Suspect 1 (GPS 1.gpx)**: Meeting occurs at **WPT 108**.
- **Suspect 2 (GPS 2.gpx)**: Meeting occurs at **WPT 182**.

### Meeting Point Coordinates

- **Latitude:** 52.3605
- **Longitude:** 4.8745

**Answer:** `52.3605;4.8745`

## Programmatic Solution

An automated approach confirms this location in under 80 milliseconds. The process:

1. **Deserialize & Sort** GPX data by time.
2. **Interpolate** positions between recorded points.
3. **Compare & Identify** intervals when both suspects are in close proximity.
4. **Output** the approximate location and time range of the meeting.

```text
GPX files deserialized and sorted.
They met! Intervals and approximate closest locations:
 - From 16:11:00 to 16:11:51, Closest Location: 52.3605;4.8745, 
   Closest Wp1: 52.3605;4.8745, Closest Wp2: 52.3605;4.8745
Total calculation time: 79.93 ms
```