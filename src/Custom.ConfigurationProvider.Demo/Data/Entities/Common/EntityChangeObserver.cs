namespace Custom.Configuration.Provider.Demo.Data.Entities.Common;

/// <summary>
/// Tells the configuration provider that entities were saved.
/// It is a singleton because configuration is built before dependency injection exists.
/// </summary>
public sealed class EntityChangeObserver
{
  private EntityChangeObserver()
  {
  }

  public static EntityChangeObserver Instance { get; } = new();

  public event EventHandler<EntityChangeEventArgs>? Changed;

  /// <summary>
  /// Raised synchronously after the changes are saved, so the next read of the configuration already sees them
  /// </summary>
  public void OnChanged(EntityChangeEventArgs e) => Changed?.Invoke(this, e);
}
