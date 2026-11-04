using MapApp.Api.Contracts;

namespace MapApp.Api.Validation;

/// <summary>A geographic position in degrees.</summary>
public readonly record struct Position(double Longitude, double Latitude);

/// <summary>
/// Validates and normalizes GeoJSON geometries coming from the client so that MongoDB's
/// <c>2dsphere</c> index accepts them (valid ranges, closed simple rings, etc.).
/// </summary>
/// <remarks>
/// Self-intersection is checked in planar longitude/latitude space. This matches what the user sees on a
/// Web Mercator map; MongoDB uses geodesic edges, which differ only for very long edges. Any geometry the
/// database still rejects is reported to the client as a 400 response as well.
/// </remarks>
public static class GeometryValidator
{
    public const int MaxPolygonVertices = 1000;

    private const double Epsilon = 1e-12;

    /// <summary>
    /// Validates a GeoJSON point. Returns the position, or <c>null</c> if invalid (errors are added).
    /// </summary>
    public static Position? ValidatePoint(PointGeometry? geometry, string path, ValidationErrors errors)
    {
        if (geometry is null)
        {
            errors.Add(path, "Geometry is required.");
            return null;
        }

        if (!string.Equals(geometry.Type, GeoJsonTypes.Point, StringComparison.Ordinal))
        {
            errors.Add($"{path}.type", $"Geometry type must be '{GeoJsonTypes.Point}'.");
            return null;
        }

        return ValidatePosition(geometry.Coordinates, $"{path}.coordinates", errors);
    }

    /// <summary>
    /// Validates a GeoJSON polygon with a single exterior ring and returns the normalized ring:
    /// consecutive duplicates removed, counter-clockwise orientation (RFC 7946) and explicitly closed
    /// (first position repeated at the end). Returns <c>null</c> if invalid (errors are added).
    /// </summary>
    public static IReadOnlyList<Position>? ValidatePolygon(PolygonGeometry? geometry, string path, ValidationErrors errors)
    {
        if (geometry is null)
        {
            errors.Add(path, "Geometry is required.");
            return null;
        }

        if (!string.Equals(geometry.Type, GeoJsonTypes.Polygon, StringComparison.Ordinal))
        {
            errors.Add($"{path}.type", $"Geometry type must be '{GeoJsonTypes.Polygon}'.");
            return null;
        }

        var coordinatesPath = $"{path}.coordinates";
        if (geometry.Coordinates is not { Length: 1 } rings || rings[0] is null)
        {
            errors.Add(coordinatesPath, "Polygon must have exactly one (exterior) ring; holes are not supported.");
            return null;
        }

        var rawRing = rings[0];
        if (rawRing.Length > MaxPolygonVertices + 1)
        {
            errors.Add(coordinatesPath, $"Polygon must have at most {MaxPolygonVertices} vertices.");
            return null;
        }

        var ring = new List<Position>(rawRing.Length + 1);
        var positionsValid = true;
        for (var i = 0; i < rawRing.Length; i++)
        {
            var position = ValidatePosition(rawRing[i], $"{coordinatesPath}[0][{i}]", errors);
            if (position is null)
            {
                positionsValid = false;
                continue;
            }

            // Collapse repeated clicks on the same spot.
            if (ring.Count == 0 || ring[^1] != position.Value)
            {
                ring.Add(position.Value);
            }
        }

        if (!positionsValid)
        {
            return null;
        }

        // Accept both open and closed rings from the client.
        if (ring.Count > 1 && ring[0] == ring[^1])
        {
            ring.RemoveAt(ring.Count - 1);
        }

        if (ring.Count < 3)
        {
            errors.Add(coordinatesPath, "Polygon must have at least 3 distinct vertices.");
            return null;
        }

        // Checked before the area: a "bow-tie" has zero net area but the real problem is the crossing.
        if (IsSelfIntersecting(ring))
        {
            errors.Add(coordinatesPath, "Polygon edges must not cross or touch each other.");
            return null;
        }

        var signedArea = SignedArea(ring);
        if (Math.Abs(signedArea) < Epsilon)
        {
            errors.Add(coordinatesPath, "Polygon must have a non-zero area (vertices must not all lie on one line).");
            return null;
        }

        if (signedArea < 0)
        {
            // Make it counter-clockwise but keep the user's first vertex first.
            ring.Reverse(1, ring.Count - 1);
        }

        ring.Add(ring[0]);
        return ring;
    }

    private static Position? ValidatePosition(double[]? coordinates, string path, ValidationErrors errors)
    {
        if (coordinates is not { Length: 2 })
        {
            errors.Add(path, "Position must be [longitude, latitude].");
            return null;
        }

        var longitude = coordinates[0];
        var latitude = coordinates[1];
        var valid = true;

        if (!double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            errors.Add(path, "Longitude must be between -180 and 180.");
            valid = false;
        }

        if (!double.IsFinite(latitude) || latitude is < -90 or > 90)
        {
            errors.Add(path, "Latitude must be between -90 and 90.");
            valid = false;
        }

        return valid ? new Position(longitude, latitude) : null;
    }

    /// <summary>Shoelace formula; positive for counter-clockwise rings. The ring must be open.</summary>
    private static double SignedArea(IReadOnlyList<Position> ring)
    {
        double sum = 0;
        for (var i = 0; i < ring.Count; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Count];
            sum += (a.Longitude * b.Latitude) - (b.Longitude * a.Latitude);
        }

        return sum / 2;
    }

    /// <summary>
    /// Returns <c>true</c> if any two edges of the (open) ring cross or touch, other than adjacent edges
    /// meeting at their shared vertex. Also detects "spikes" where an edge folds back over the previous one.
    /// </summary>
    internal static bool IsSelfIntersecting(IReadOnlyList<Position> ring)
    {
        var n = ring.Count;

        for (var i = 0; i < n; i++)
        {
            var a1 = ring[i];
            var a2 = ring[(i + 1) % n];

            // Adjacent edges (a1->a2, a2->a3) are invalid only if the second one doubles back along the first.
            var a3 = ring[(i + 2) % n];
            if (Sign(Cross(a1, a2, a3)) == 0 && Dot(a1, a2, a3) < 0)
            {
                return true;
            }

            // Non-adjacent edges must not share any point.
            for (var j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1)
                {
                    continue; // The closing edge is adjacent to the first edge.
                }

                if (SegmentsIntersect(a1, a2, ring[j], ring[(j + 1) % n]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(Position p1, Position p2, Position q1, Position q2)
    {
        var d1 = Sign(Cross(q1, q2, p1));
        var d2 = Sign(Cross(q1, q2, p2));
        var d3 = Sign(Cross(p1, p2, q1));
        var d4 = Sign(Cross(p1, p2, q2));

        // Proper crossing: each segment's endpoints lie strictly on opposite sides of the other.
        if (d1 * d2 < 0 && d3 * d4 < 0)
        {
            return true;
        }

        // Touching or collinear overlap: an endpoint lies on the other segment.
        return (d1 == 0 && OnSegment(q1, q2, p1))
            || (d2 == 0 && OnSegment(q1, q2, p2))
            || (d3 == 0 && OnSegment(p1, p2, q1))
            || (d4 == 0 && OnSegment(p1, p2, q2));
    }

    /// <summary>Z component of (b - a) x (c - a).</summary>
    private static double Cross(Position a, Position b, Position c) =>
        ((b.Longitude - a.Longitude) * (c.Latitude - a.Latitude)) - ((b.Latitude - a.Latitude) * (c.Longitude - a.Longitude));

    /// <summary>Dot product of (b - a) and (c - b).</summary>
    private static double Dot(Position a, Position b, Position c) =>
        ((b.Longitude - a.Longitude) * (c.Longitude - b.Longitude)) + ((b.Latitude - a.Latitude) * (c.Latitude - b.Latitude));

    private static int Sign(double value) => Math.Abs(value) < Epsilon ? 0 : Math.Sign(value);

    /// <summary>Checks whether <paramref name="p"/> (assumed collinear with a-b) lies within segment a-b.</summary>
    private static bool OnSegment(Position a, Position b, Position p) =>
        p.Longitude >= Math.Min(a.Longitude, b.Longitude) - Epsilon
        && p.Longitude <= Math.Max(a.Longitude, b.Longitude) + Epsilon
        && p.Latitude >= Math.Min(a.Latitude, b.Latitude) - Epsilon
        && p.Latitude <= Math.Max(a.Latitude, b.Latitude) + Epsilon;
}
