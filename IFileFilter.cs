namespace BackgroundWorkerAgent;

public interface IFileFilter
{
    bool IsAllowed(string filePath);
}