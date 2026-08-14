namespace CareOps.Application.Abstractions;

public interface IFileMetadataStorage
{
    string CreateStorageKey(Guid providerId, string originalFileName);
}
