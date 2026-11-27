namespace Nexus.Domain.Abstractions;

public interface IEntity
{
    DateTime CreatedAt { get; set; }
    long? CreatedById { get; set; }
    DateTime? LastModifiedAt { get; set; }
    long? LastModifiedById { get; set; }
}

public interface IEntity<T> : IEntity
{
    T Id { get; set; }
}

/// <summary>Base for every table that carries an id and who/when audit columns. The DbContext fills the audit columns.</summary>
public abstract class Entity<T> : IEntity<T>
{
    public T Id { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public long? LastModifiedById { get; set; }
}
