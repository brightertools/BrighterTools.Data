namespace BrighterTools.Data.Abstractions.Entities;

/// <summary>
/// Entity contract for entities with a key of type <typeparamref name="TKey"/>.
/// </summary>
public interface IEntity<TKey>
{
    TKey Id { get; }
}
