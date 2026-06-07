using NSubstitute;
using RaceTracker.Management.Application.Abstractions.Crud;
using RaceTracker.Management.Application.Crud;
using RaceTracker.Management.Application.Observability;
using RaceTracker.Management.Domain.Abstractions;
using Shouldly;
using Xunit;

namespace RaceTracker.Management.UnitTests;

/// <summary>
/// Unit tests for the generic CRUD use case (<c>/A70/</c>, story 5.3) with its ports mocked. Proves
/// the single-commit boundary (every mutation calls <c>SaveChangesAsync</c> exactly once) and that
/// updates/deletes of a missing entity are a no-op 404 — no staged write, no commit.
/// </summary>
public sealed class CrudServiceTests : IDisposable
{
    private readonly IRepository<FakeEntity> _repository = Substitute.For<IRepository<FakeEntity>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ManagementMetrics _metrics = new();

    public void Dispose() => _metrics.Dispose();

    private CrudService<FakeEntity> CreateSut() => new(_repository, _unitOfWork, _metrics);

    private static FakeEntity AnEntity(string id = "entity-1") => new() { Id = id };

    [Fact]
    public async Task Create_of_a_new_entity_stages_it_and_commits_once()
    {
        FakeEntity entity = AnEntity();
        // No entity with this id exists yet (the repository returns null by default).

        bool created = await CreateSut().CreateAsync(entity, CancellationToken.None);

        created.ShouldBeTrue();
        _repository.Received(1).Add(entity);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_of_an_existing_id_is_a_no_op_409_without_commit()
    {
        FakeEntity entity = AnEntity();
        _repository.GetByIdAsync("entity-1", Arg.Any<CancellationToken>()).Returns(entity);

        bool created = await CreateSut().CreateAsync(entity, CancellationToken.None);

        created.ShouldBeFalse();
        _repository.DidNotReceive().Add(Arg.Any<FakeEntity>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_returns_the_repository_entity()
    {
        FakeEntity entity = AnEntity();
        _repository.GetByIdAsync("entity-1", Arg.Any<CancellationToken>()).Returns(entity);

        FakeEntity? result = await CreateSut().GetByIdAsync("entity-1", CancellationToken.None);

        result.ShouldBe(entity);
    }

    [Fact]
    public async Task GetById_returns_null_when_the_entity_is_missing()
    {
        _repository.GetByIdAsync("ghost", Arg.Any<CancellationToken>()).Returns((FakeEntity?)null);

        FakeEntity? result = await CreateSut().GetByIdAsync("ghost", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAll_returns_the_repository_list()
    {
        IReadOnlyList<FakeEntity> entities = [AnEntity("a"), AnEntity("b")];
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entities);

        IReadOnlyList<FakeEntity> result = await CreateSut().GetAllAsync(CancellationToken.None);

        result.ShouldBe(entities);
    }

    [Fact]
    public async Task Update_of_an_existing_entity_applies_stages_and_commits_once()
    {
        FakeEntity existing = AnEntity();
        FakeEntity applied = AnEntity();
        _repository.GetByIdAsync("entity-1", Arg.Any<CancellationToken>()).Returns(existing);

        FakeEntity? updated = await CreateSut().UpdateAsync("entity-1", _ => applied, CancellationToken.None);

        updated.ShouldBe(applied);
        _repository.Received(1).Update(applied);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_of_a_missing_entity_is_a_no_op_404_without_commit()
    {
        _repository.GetByIdAsync("ghost", Arg.Any<CancellationToken>()).Returns((FakeEntity?)null);

        FakeEntity? updated = await CreateSut().UpdateAsync("ghost", _ => AnEntity(), CancellationToken.None);

        updated.ShouldBeNull();
        _repository.DidNotReceive().Update(Arg.Any<FakeEntity>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_of_an_existing_entity_stages_removal_and_commits_once()
    {
        _repository.GetByIdAsync("entity-1", Arg.Any<CancellationToken>()).Returns(AnEntity());

        bool deleted = await CreateSut().DeleteAsync("entity-1", CancellationToken.None);

        deleted.ShouldBeTrue();
        _repository.Received(1).Remove("entity-1");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_of_a_missing_entity_is_a_no_op_404_without_commit()
    {
        _repository.GetByIdAsync("ghost", Arg.Any<CancellationToken>()).Returns((FakeEntity?)null);

        bool deleted = await CreateSut().DeleteAsync("ghost", CancellationToken.None);

        deleted.ShouldBeFalse();
        _repository.DidNotReceive().Remove(Arg.Any<string>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Minimal <see cref="IEntity"/> so the generic service is tested without a real entity. Public
    /// so NSubstitute/Castle can proxy <c>IRepository&lt;FakeEntity&gt;</c>.
    /// </summary>
    public sealed class FakeEntity : IEntity
    {
        public required string Id { get; init; }
    }
}
