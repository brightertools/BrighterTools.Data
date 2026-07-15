namespace BrighterTools.Data.EFCore.Abstractions;

public interface ITenantContextAccessor
{
    int CurrentTenantId { get; }
}
