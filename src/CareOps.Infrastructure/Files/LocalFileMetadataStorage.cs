using CareOps.Application.Abstractions;

namespace CareOps.Infrastructure.Files;

public sealed class LocalFileMetadataStorage : IFileMetadataStorage
{
    public string CreateStorageKey(Guid providerId, string originalFileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(originalFileName)).ToLowerInvariant();
        return $"providers/{providerId:N}/{Guid.NewGuid():N}{extension}";
    }
}
