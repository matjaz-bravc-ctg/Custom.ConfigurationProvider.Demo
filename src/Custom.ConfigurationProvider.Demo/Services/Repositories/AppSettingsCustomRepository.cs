using Custom.Configuration.Provider.Demo.Data;
using Custom.Configuration.Provider.Demo.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Custom.Configuration.Provider.Demo.Services.Repositories;

/// <summary>
/// Keeps exactly one default configuration whenever there is at least one configuration.
/// Each change is saved with a single SaveChanges, so the configuration provider reloads once per change.
/// </summary>
public sealed class AppSettingsCustomRepository(DatabaseContext dbContext) : IAppSettingsCustomRepository
{
  public async Task<IReadOnlyList<AppSettingsCustomEntity>> GetAllAsync(CancellationToken cancellationToken = default) =>
    await dbContext.AppSettingsCustomItems
      .AsNoTracking()
      .OrderBy(entity => entity.Id)
      .ToListAsync(cancellationToken);

  public Task<AppSettingsCustomEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
    dbContext.AppSettingsCustomItems
      .AsNoTracking()
      .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

  public async Task AddAsync(AppSettingsCustomEntity entity, CancellationToken cancellationToken = default)
  {
    List<AppSettingsCustomEntity> all = await LoadAllAsync(cancellationToken);
    entity.Id = 0;
    dbContext.AppSettingsCustomItems.Add(entity);
    all.Add(entity);
    KeepSingleDefault(all, entity.Default ? entity : null);
    await dbContext.SaveChangesAsync(cancellationToken);
  }

  public async Task<bool> UpdateAsync(AppSettingsCustomEntity entity, CancellationToken cancellationToken = default)
  {
    List<AppSettingsCustomEntity> all = await LoadAllAsync(cancellationToken);
    AppSettingsCustomEntity? existing = all.Find(item => item.Id == entity.Id);
    if (existing is null)
    {
      return false;
    }

    existing.CustomSettingA = entity.CustomSettingA;
    existing.CustomSettingB = entity.CustomSettingB;
    existing.Default = entity.Default;
    KeepSingleDefault(all, existing.Default ? existing : null);
    await dbContext.SaveChangesAsync(cancellationToken);
    return true;
  }

  public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
  {
    List<AppSettingsCustomEntity> all = await LoadAllAsync(cancellationToken);
    AppSettingsCustomEntity? existing = all.Find(item => item.Id == id);
    if (existing is null)
    {
      return false;
    }

    dbContext.AppSettingsCustomItems.Remove(existing);
    all.Remove(existing);
    KeepSingleDefault(all, preferred: null);
    await dbContext.SaveChangesAsync(cancellationToken);
    return true;
  }

  public async Task<bool> SetDefaultAsync(int id, CancellationToken cancellationToken = default)
  {
    List<AppSettingsCustomEntity> all = await LoadAllAsync(cancellationToken);
    AppSettingsCustomEntity? existing = all.Find(item => item.Id == id);
    if (existing is null)
    {
      return false;
    }

    KeepSingleDefault(all, existing);
    await dbContext.SaveChangesAsync(cancellationToken);
    return true;
  }

  // The table holds a handful of rows, so loading them all keeps the default handling simple
  private Task<List<AppSettingsCustomEntity>> LoadAllAsync(CancellationToken cancellationToken) =>
    dbContext.AppSettingsCustomItems.ToListAsync(cancellationToken);

  /// <summary>
  /// Makes <paramref name="preferred"/> the only default. Without one, keeps the current default,
  /// or makes the oldest configuration the default when there is none.
  /// </summary>
  private static void KeepSingleDefault(List<AppSettingsCustomEntity> all, AppSettingsCustomEntity? preferred)
  {
    AppSettingsCustomEntity? chosen = preferred
      ?? all.Where(entity => entity.Default).OrderBy(entity => entity.Id).FirstOrDefault()
      ?? all.OrderBy(entity => entity.Id).FirstOrDefault();
    foreach (AppSettingsCustomEntity entity in all)
    {
      entity.Default = ReferenceEquals(entity, chosen);
    }
  }
}
