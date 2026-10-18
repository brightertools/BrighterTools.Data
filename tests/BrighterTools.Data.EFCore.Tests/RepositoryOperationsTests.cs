using BrighterTools.Data.Abstractions.Entities;
using BrighterTools.Data.EFCore.Abstractions;
using BrighterTools.Data.EFCore.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrighterTools.Data.EFCore.Tests;

public sealed class RepositoryOperationsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LookupHonorsTrackingAndHostQueryFilters(bool tracking)
    {
        using var db = CreateDb();
        var repository = new ProbeRepository(db);
        var found = await repository.Find("visible", tracking);
        Assert.NotNull(found);
        Assert.Equal(tracking ? EntityState.Unchanged : EntityState.Detached, db.Entry(found).State);
        Assert.Null(await repository.Find("other tenant", tracking));
        Assert.Null(await repository.Find("deleted", tracking));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PropertyUpdatesUseAuditedPersistenceHook(bool byLookup)
    {
        using var db = CreateDb();
        var repository = new ProbeRepository(db);
        var updated = byLookup
            ? await repository.SetByName("visible", "changed", 42)
            : await repository.SetName(1, "changed", 42);
        Assert.True(updated);
        Assert.Equal(1, repository.PersistCalls);
        Assert.Equal(42, repository.AuditUserId);
        db.ChangeTracker.Clear();
        Assert.Equal("changed", (await db.Rows.SingleAsync(x => x.Id == 1)).Name);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public async Task HiddenOrMissingEntitiesCannotBeUpdatedOrDeleted(int id)
    {
        using var db = CreateDb();
        var repository = new ProbeRepository(db);
        Assert.False(await repository.SetName(id, "changed", 42));
        Assert.False(await repository.Remove(id));
        Assert.Equal(0, repository.PersistCalls);
        Assert.Equal(3, await db.Rows.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task LookupUpdateDoesNotBypassHostScope()
    {
        using var db = CreateDb();
        var repository = new ProbeRepository(db);
        Assert.False(await repository.SetByName("other tenant", "changed", 42));
        Assert.Equal(0, repository.PersistCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletePreservesSoftAndExplicitHardDeleteSemantics(bool hard)
    {
        using var db = CreateDb();
        var repository = new ProbeRepository(db);
        Assert.True(await repository.Remove(1, hard));
        db.ChangeTracker.Clear();
        var row = await db.Rows.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == 1);
        if (hard) Assert.Null(row);
        else Assert.True(row!.Deleted);
        Assert.Null(await repository.Find("visible", false));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public async Task SavePreservesTenantGuardAndHostHooks(int entityTenant, bool allowed)
    {
        using var db = CreateDb();
        var repository = new ProbeRepository(db);
        var row = new Row { Name = "new", TenantId = entityTenant };
        if (!allowed)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SaveAsync(row, 42));
            Assert.Equal(0, repository.PersistCalls);
            Assert.Equal(EntityState.Detached, db.Entry(row).State);
            return;
        }
        await repository.SaveAsync(row, 42);
        Assert.Equal(7, row.TenantId);
        Assert.Equal(42, repository.AuditUserId);
        Assert.Equal(1, repository.BeforeCalls);
        Assert.Equal(1, repository.AfterCalls);
    }

    [Fact]
    public async Task InvalidPropertySelectorDoesNotSave()
    {
        using var db = CreateDb();
        var repository = new ProbeRepository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InvalidSetter());
        Assert.Equal(0, repository.PersistCalls);
    }

    private static TestDb CreateDb()
    {
        var db = new TestDb(new DbContextOptionsBuilder<TestDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Rows.AddRange(
            new Row { Id = 1, TenantId = 7, Name = "visible" },
            new Row { Id = 2, TenantId = 8, Name = "other tenant" },
            new Row { Id = 3, TenantId = 7, Name = "deleted", Deleted = true });
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return db;
    }

    private sealed class Row : IEntity<int>
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = "";
        public bool Deleted { get; set; }
    }

    private sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options)
    {
        public DbSet<Row> Rows => Set<Row>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().HasQueryFilter(x => x.TenantId == 7 && !x.Deleted);
    }

    private sealed class Tenant : ITenantContextAccessor
    {
        public int CurrentTenantId => 7;
    }

    private sealed class ProbeRepository(TestDb db)
        : BaseRepository<Row, int, int>(db, NullLogger.Instance, new Tenant())
    {
        public int PersistCalls { get; private set; }
        public int BeforeCalls { get; private set; }
        public int AfterCalls { get; private set; }
        public int? AuditUserId { get; private set; }
        public Task<Row?> Find(string name, bool tracking) => GetByPropertyAsync(x => x.Name, name, tracking);
        public Task<bool> SetName(int id, string value, int user) => SetPropertyByIdAsync(id, x => x.Name, value, user);
        public Task<bool> SetByName(string name, string value, int user)
            => SetPropertyByLookupAsync(x => x.Name, name, x => x.Name, value, user);
        public Task<bool> Remove(int id, bool hard = false) => DeleteAsync(id, hard);
        public Task<bool> InvalidSetter() => SetPropertyByIdAsync(1, x => x.Name.ToUpper(), "changed");
        protected override Task<int> PersistEntityAsync(Row entity, int? userId, CancellationToken cancellationToken)
        {
            PersistCalls++;
            AuditUserId = userId;
            return db.SaveChangesAsync(cancellationToken);
        }
        protected override Task OnBeforeSaveInternal(Row entity)
        {
            BeforeCalls++;
            return Task.CompletedTask;
        }
        protected override Task OnAfterSave(Row entity, int affectedRows)
        {
            AfterCalls++;
            return Task.CompletedTask;
        }
    }
}
