namespace IterateFiles.Model;

public class FileModel
{
    public string? Name { get; set; }
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
}