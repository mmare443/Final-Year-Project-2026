namespace LCC_CMS_Api.Services;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _rootPath;

    public LocalFileStorage(IWebHostEnvironment environment)
    {
        _rootPath = Path.Combine(environment.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string category,
        string extension,
        string? originalFileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        var safeCategory = ValidateSegment(category, nameof(category));
        var safeExtension = NormalizeExtension(extension);
        var categoryPath = Path.Combine(_rootPath, safeCategory);
        Directory.CreateDirectory(categoryPath);

        var fileName = $"{Guid.NewGuid():N}{safeExtension}";
        var fullPath = Path.Combine(categoryPath, fileName);
        var storageKey = $"{safeCategory}/{fileName}";

        await using var output = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);
        await content.CopyToAsync(output, cancellationToken);

        return new StoredFile(
            storageKey,
            Path.GetFileName(originalFileName ?? fileName),
            contentType,
            output.Length);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var relativePath = NormalizeStorageKey(storageKey);
        var fullPath = GetFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string GetFullPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The storage key points outside the file storage root.", nameof(relativePath));
        }

        return fullPath;
    }

    private static string ValidateSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value is "." or ".."
            || value.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            throw new ArgumentException("The storage category is invalid.", parameterName);
        }

        return value.Trim();
    }

    private static string NormalizeExtension(string extension)
    {
        var value = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length < 2
            || value[0] != '.'
            || value.IndexOfAny(new[] { '/', '\\' }) >= 0
            || value.Any(c => !char.IsLetterOrDigit(c) && c != '.'))
        {
            throw new ArgumentException("The file extension is invalid.", nameof(extension));
        }

        return value;
    }

    private static string NormalizeStorageKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("The storage key is required.", nameof(storageKey));
        }

        var normalized = storageKey.Replace('\\', '/').TrimStart('/');
        if (normalized.Split('/').Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(storageKey));
        }

        return normalized;
    }
}
