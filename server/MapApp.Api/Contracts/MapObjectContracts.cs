using MapApp.Api.Models;

namespace MapApp.Api.Contracts;

/// <summary>A map object returned by the API.</summary>
public sealed record MapObjectDto(string Id, string Name, MapObjectSymbol Symbol, PointGeometry Geometry, DateTime CreatedAt);

/// <summary>
/// A map object to create. <paramref name="ClientId"/> is an optional idempotency key: saving the same key
/// again stores nothing new.
/// </summary>
public sealed record CreateMapObjectRequest(
    string? Name, MapObjectSymbol? Symbol, PointGeometry? Geometry, string? ClientId = null);
