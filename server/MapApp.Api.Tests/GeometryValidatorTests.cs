using MapApp.Api.Contracts;
using MapApp.Api.Validation;

namespace MapApp.Api.Tests;

public sealed class GeometryValidatorTests
{
    private static PolygonGeometry Polygon(params double[][] ring) => new(GeoJsonTypes.Polygon, [ring]);

    private static double[] P(double lon, double lat) => [lon, lat];

    private static (IReadOnlyList<Position>? Ring, ValidationErrors Errors) Validate(PolygonGeometry? geometry)
    {
        var errors = new ValidationErrors();
        return (GeometryValidator.ValidatePolygon(geometry, "geometry", errors), errors);
    }

    /// <summary>A regular n-gon (always simple), optionally closed.</summary>
    private static PolygonGeometry RegularPolygon(int vertices, bool closed)
    {
        var ring = Enumerable.Range(0, vertices)
            .Select(i => P(Math.Cos(2 * Math.PI * i / vertices), Math.Sin(2 * Math.PI * i / vertices)))
            .ToList();
        if (closed)
        {
            ring.Add(ring[0]);
        }

        return Polygon([.. ring]);
    }

    [Theory]
    [InlineData(1000, false, true)]
    [InlineData(1000, true, true)]   // 1001 positions, but the last one only closes the ring
    [InlineData(1001, false, false)] // used to pass: 1001 positions did not exceed the "+1" guard
    [InlineData(1001, true, false)]
    public void Vertex_limit_counts_distinct_vertices(int vertices, bool closed, bool accepted)
    {
        var (ring, errors) = Validate(RegularPolygon(vertices, closed));

        Assert.Equal(accepted, !errors.HasErrors);
        Assert.Equal(accepted, ring is not null);
    }

    [Fact]
    public void Open_ring_is_closed_and_oriented_counter_clockwise()
    {
        // Clockwise square, not closed.
        var (ring, errors) = Validate(Polygon(P(0, 0), P(0, 1), P(1, 1), P(1, 0)));

        Assert.False(errors.HasErrors);
        Assert.NotNull(ring);
        Assert.Equal(5, ring.Count);
        Assert.Equal(ring[0], ring[^1]);
        Assert.Equal(new Position(1, 0), ring[1]); // reversed to counter-clockwise
    }

    [Fact]
    public void Already_closed_ring_is_not_closed_twice()
    {
        var (ring, errors) = Validate(Polygon(P(0, 0), P(1, 0), P(1, 1), P(0, 0)));

        Assert.False(errors.HasErrors);
        Assert.Equal(4, ring!.Count);
    }

    [Fact]
    public void Consecutive_duplicate_vertices_are_collapsed()
    {
        var (ring, errors) = Validate(Polygon(P(0, 0), P(0, 0), P(1, 0), P(1, 0), P(1, 1)));

        Assert.False(errors.HasErrors);
        Assert.Equal(4, ring!.Count);
    }

    [Fact]
    public void Fewer_than_three_distinct_vertices_is_rejected()
    {
        var (ring, errors) = Validate(Polygon(P(0, 0), P(1, 1), P(0, 0)));

        Assert.Null(ring);
        Assert.True(errors.HasErrors);
    }

    [Fact]
    public void Bow_tie_is_rejected_as_self_intersecting()
    {
        var (ring, errors) = Validate(Polygon(P(0, 0), P(1, 1), P(1, 0), P(0, 1)));

        Assert.Null(ring);
        Assert.Contains("cross", errors.ToDictionary()["geometry.coordinates"][0]);
    }

    [Fact]
    public void Ring_touching_itself_at_a_vertex_is_rejected()
    {
        // Two triangles sharing vertex (1,1).
        var (ring, _) = Validate(Polygon(P(0, 0), P(1, 1), P(2, 0), P(2, 2), P(1, 1), P(0, 2)));

        Assert.Null(ring);
    }

    [Fact]
    public void Collinear_vertices_are_rejected()
    {
        var (ring, errors) = Validate(Polygon(P(0, 0), P(1, 1), P(2, 2)));

        Assert.Null(ring);
        Assert.True(errors.HasErrors);
    }

    [Fact]
    public void Concave_polygon_is_accepted()
    {
        var (ring, errors) = Validate(Polygon(P(0, 0), P(4, 0), P(4, 4), P(2, 1), P(0, 4)));

        Assert.False(errors.HasErrors);
        Assert.NotNull(ring);
    }

    [Theory]
    [InlineData(181, 0)]
    [InlineData(-181, 0)]
    [InlineData(0, 91)]
    [InlineData(0, -91)]
    [InlineData(double.NaN, 0)]
    public void Out_of_range_positions_are_rejected(double lon, double lat)
    {
        var (ring, errors) = Validate(Polygon(P(lon, lat), P(1, 0), P(1, 1)));

        Assert.Null(ring);
        Assert.True(errors.ToDictionary().ContainsKey("geometry.coordinates[0][0]"));
    }

    [Fact]
    public void Holes_are_not_supported()
    {
        var geometry = new PolygonGeometry(GeoJsonTypes.Polygon,
        [
            [P(0, 0), P(10, 0), P(10, 10), P(0, 0)],
            [P(1, 1), P(2, 1), P(2, 2), P(1, 1)],
        ]);

        var (ring, _) = Validate(geometry);

        Assert.Null(ring);
    }

    [Fact]
    public void Wrong_geometry_type_is_rejected()
    {
        var (ring, errors) = Validate(new PolygonGeometry("LineString", [[P(0, 0), P(1, 0), P(1, 1)]]));

        Assert.Null(ring);
        Assert.True(errors.ToDictionary().ContainsKey("geometry.type"));
    }

    [Fact]
    public void Point_with_altitude_or_missing_coordinate_is_rejected()
    {
        var errors = new ValidationErrors();

        Assert.Null(GeometryValidator.ValidatePoint(new PointGeometry(GeoJsonTypes.Point, [1, 2, 3]), "g", errors));
        Assert.Null(GeometryValidator.ValidatePoint(new PointGeometry(GeoJsonTypes.Point, [1]), "g", errors));
        Assert.Null(GeometryValidator.ValidatePoint(null, "g", errors));
    }

    [Fact]
    public void Valid_point_is_accepted()
    {
        var errors = new ValidationErrors();

        var position = GeometryValidator.ValidatePoint(new PointGeometry(GeoJsonTypes.Point, [34.78, 32.08]), "g", errors);

        Assert.Equal(new Position(34.78, 32.08), position);
        Assert.False(errors.HasErrors);
    }
}
