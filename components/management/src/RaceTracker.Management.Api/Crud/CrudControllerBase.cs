using Microsoft.AspNetCore.Mvc;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Domain.Abstractions;

namespace RaceTracker.Management.Api.Crud;

/// <summary>
/// Generic CRUD controller (<c>/A70/</c>: the project's realization of <c>Controller&lt;T&gt;</c>):
/// the five REST endpoints implemented <b>once</b> over the generic <see cref="CrudService{TEntity}"/>.
/// It is parameterized additionally by the request/response DTO types so the wire contract stays
/// <b>separate from the entity</b> (§8 Wartbarkeit) — concrete controllers (Vehicle, story 5.3) just
/// bind the four types and supply the small map hooks. Carries no <c>[AllowAnonymous]</c>, so the
/// fallback authorization policy protects every action (secure-by-default, <c>/F12/</c>).
/// </summary>
[ApiController]
public abstract class CrudControllerBase<TEntity, TCreate, TUpdate, TResponse> : ControllerBase
    where TEntity : IEntity
{
    private readonly CrudService<TEntity> _service;

    protected CrudControllerBase(CrudService<TEntity> service) => _service = service;

    /// <summary>Builds a fresh entity from a create request (concrete controller owns the mapping).</summary>
    protected abstract TEntity ToEntity(TCreate request);

    /// <summary>Applies an update request onto an existing entity (identity stays fixed).</summary>
    protected abstract TEntity Apply(TUpdate request, TEntity existing);

    /// <summary>Projects an entity to its response DTO.</summary>
    protected abstract TResponse ToResponse(TEntity entity);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TResponse>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<TEntity> entities = await _service.GetAllAsync(cancellationToken);
        return Ok(entities.Select(ToResponse).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TResponse>> GetById(string id, CancellationToken cancellationToken)
    {
        TEntity? entity = await _service.GetByIdAsync(id, cancellationToken);
        return entity is null ? NotFound() : Ok(ToResponse(entity));
    }

    [HttpPost]
    public async Task<ActionResult<TResponse>> Create(TCreate request, CancellationToken cancellationToken)
    {
        TEntity entity = ToEntity(request);
        bool created = await _service.CreateAsync(entity, cancellationToken);
        return created
            ? CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToResponse(entity))
            : Conflict();
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TResponse>> Update(
        string id, TUpdate request, CancellationToken cancellationToken)
    {
        TEntity? updated = await _service.UpdateAsync(
            id, existing => Apply(request, existing), cancellationToken);
        return updated is null ? NotFound() : Ok(ToResponse(updated));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        bool deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
