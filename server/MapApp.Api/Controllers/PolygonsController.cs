using MapApp.Api.Contracts;
using MapApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MapApp.Api.Controllers;

/// <summary>
/// REST endpoints for polygons. Geometries use GeoJSON (<c>[longitude, latitude]</c>).
/// </summary>
[ApiController]
[Route("api/polygons")]
public sealed class PolygonsController(IPolygonService service) : ControllerBase
{
    /// <summary>Returns all saved polygons.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PolygonDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PolygonDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    /// <summary>Returns a single polygon.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<PolygonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PolygonDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var polygon = await service.GetByIdAsync(id, cancellationToken);
        return polygon is null ? NotFound() : Ok(polygon);
    }

    /// <summary>
    /// Saves one or more new polygons and returns the full, updated list of polygons.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<IReadOnlyList<PolygonDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PolygonDto>>> Create(
        [FromBody] List<CreatePolygonRequest> polygons,
        CancellationToken cancellationToken) =>
        Ok(await service.CreateManyAsync(polygons, cancellationToken));

    /// <summary>Deletes a single polygon.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Deletes all polygons.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        await service.DeleteAllAsync(cancellationToken);
        return NoContent();
    }
}
