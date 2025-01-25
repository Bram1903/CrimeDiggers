using System.Xml.Serialization;

namespace geo.models;

[XmlRoot(ElementName = "gpx")]
public class Gpx
{
    [XmlElement(ElementName = "wpt")] public required List<Waypoint> Waypoints { get; set; }
}

public class Waypoint
{
    [XmlAttribute(AttributeName = "lat")] public double Latitude { get; set; }

    [XmlAttribute(AttributeName = "lon")] public double Longitude { get; set; }

    [XmlElement(ElementName = "time")] public DateTime Time { get; set; }
}