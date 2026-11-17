namespace Custom.Configuration.Provider.Demo.Data.Entities.Common;

public sealed class EntityChangeEventArgs(IReadOnlySet<Type> entityTypes) : EventArgs
{
  /// <summary>
  /// Types of the entities that were added, modified or deleted
  /// </summary>
  public IReadOnlySet<Type> EntityTypes { get; } = entityTypes;
}
