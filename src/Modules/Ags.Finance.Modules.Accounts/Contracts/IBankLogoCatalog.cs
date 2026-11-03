namespace Ags.Finance.Modules.Accounts.Contracts;

public sealed record LogoCrop(double Zoom = 1, double Horizontal = 0, double Vertical = 0);
public sealed record LogoPreviewResult(byte[]? Png, string? Error)
{
    public bool Succeeded => Png is not null && Error is null;
}
public sealed record BankLogoDetails(byte[] Png, Guid Version);
public interface IBankLogoCatalog
{
    const int MaxSourceBytes = 5 * 1024 * 1024;
    LogoPreviewResult CreatePreview(byte[] content, LogoCrop crop);
    Task<CatalogResult> SaveAsync(Guid bankExternalId, byte[] content, LogoCrop crop, CancellationToken cancellationToken = default);
    Task<CatalogResult> RemoveAsync(Guid bankExternalId, CancellationToken cancellationToken = default);
    Task<BankLogoDetails?> GetAsync(Guid bankExternalId, CancellationToken cancellationToken = default);
}
