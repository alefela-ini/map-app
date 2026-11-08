using MapApp.Api.Contracts;
using MapApp.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MapApp.Api.Controllers;

/// <summary>
/// REST endpoints for map objects (standard markers and custom symbols such as a Jeep).
/// Locations use GeoJSON points (<c>[longitude, latitude]</c>).
/// </summary>
[ApiController]
[Route("api/objects")]
public sealed class ObjectsController(IMapObjectService service) : ControllerBase
{
    /// <summary>Returns all saved objects.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MapObjectDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MapObjectDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    /// <summary>Returns a single object.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<MapObjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MapObjectDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var mapObject = await service.GetByIdAsync(id, cancellationToken);
        return mapObject is null ? NotFound() : Ok(mapObject);
    }

    /// <summary>
    /// Saves a list of new objects and returns the saved objects (this batch only, in request order).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<IReadOnlyList<MapObjectDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MapObjectDto>>> Create(
        [FromBody] List<CreateMapObjectRequest> objects,
        CancellationToken cancellationToken) =>
        Ok(await service.CreateManyAsync(objects, cancellationToken));

    /// <summary>Deletes a single object.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Deletes all objects.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        await service.DeleteAllAsync(cancellationToken);
        return NoContent();
    }
}
