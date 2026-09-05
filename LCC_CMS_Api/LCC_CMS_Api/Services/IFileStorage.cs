namespace LCC_CMS_Api.Services;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Stream content,
        string category,
        string extension,
        string? originalFileName,
        string? contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed record StoredFile(
    string StorageKey,
    string OriginalFileName,
    string? ContentType,
    long Length);
