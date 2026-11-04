namespace MapApp.Api.Contracts;

/// <summary>A polygon returned by the API.</summary>
public sealed record PolygonDto(string Id, string Name, PolygonGeometry Geometry, DateTime CreatedAt);

/// <summary>
/// A polygon to create. The ring may be open or closed; the server closes it before saving.
/// </summary>
public sealed record CreatePolygonRequest(string? Name, PolygonGeometry? Geometry);
