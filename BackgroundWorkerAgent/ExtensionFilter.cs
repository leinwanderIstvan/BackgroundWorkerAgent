namespace BackgroundWorkerAgent;

public class ExtensionFilter : IFileFilter
{
    private readonly HashSet<string> _allowedExtensions;

    public ExtensionFilter(FileFilterOption options)
    {
        _allowedExtensions = new HashSet<string>(options.AllowedExtensions.Select(NormalizeExtension), StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowed(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return _allowedExtensions.Contains(Path.GetExtension(filePath));
    }

    private static string NormalizeExtension(string ext) => ext.StartsWith('.') ? ext : $".{ext}";
}
