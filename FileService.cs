namespace BackgroundWorkerAgent;

public class FileService
{
    private readonly IFileFilter _filter;

    public FileService(IFileFilter filter)
    {
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    public IEnumerable<string> GetFilteredFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found : {directory}");
        }

        return Directory.EnumerateFiles(directory).Where(_filter.IsAllowed);
    }
}
