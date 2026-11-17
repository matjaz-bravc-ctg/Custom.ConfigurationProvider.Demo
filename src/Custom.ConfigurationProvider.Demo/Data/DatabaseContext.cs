using Custom.Configuration.Provider.Demo.Data.Entities;
using Custom.Configuration.Provider.Demo.Data.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace Custom.Configuration.Provider.Demo.Data;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
  public DbSet<AppSettingsCustomEntity> AppSettingsCustomItems => Set<AppSettingsCustomEntity>();

  // Every SaveChanges and SaveChangesAsync overload ends up in one of these two overrides
  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    HashSet<Type> changedEntityTypes = GetChangedEntityTypes();
    int result = base.SaveChanges(acceptAllChangesOnSuccess);
    NotifyChanged(changedEntityTypes);
    return result;
  }

  public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
  {
    HashSet<Type> changedEntityTypes = GetChangedEntityTypes();
    int result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    NotifyChanged(changedEntityTypes);
    return result;
  }

  private HashSet<Type> GetChangedEntityTypes() => ChangeTracker.Entries()
    .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
    .Select(entry => entry.Entity.GetType())
    .ToHashSet();

  // Only after the save succeeded, and once per save (not once per changed row)
  private static void NotifyChanged(HashSet<Type> changedEntityTypes)
  {
    if (changedEntityTypes.Count > 0)
    {
      EntityChangeObserver.Instance.OnChanged(new EntityChangeEventArgs(changedEntityTypes));
    }
  }
}
