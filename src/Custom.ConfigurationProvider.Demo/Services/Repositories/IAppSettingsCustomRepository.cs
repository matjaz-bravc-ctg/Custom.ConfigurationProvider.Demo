using Custom.Configuration.Provider.Demo.Data.Entities;

namespace Custom.Configuration.Provider.Demo.Services.Repositories;

public interface IAppSettingsCustomRepository
{
  Task<IReadOnlyList<AppSettingsCustomEntity>> GetAllAsync(CancellationToken cancellationToken = default);

  Task<AppSettingsCustomEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

  Task AddAsync(AppSettingsCustomEntity entity, CancellationToken cancellationToken = default);

  /// <returns>False when no configuration has the entity's id</returns>
  Task<bool> UpdateAsync(AppSettingsCustomEntity entity, CancellationToken cancellationToken = default);

  /// <returns>False when no configuration has the id</returns>
  Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

  /// <returns>False when no configuration has the id</returns>
  Task<bool> SetDefaultAsync(int id, CancellationToken cancellationToken = default);
}
