namespace Samco_HSE_Manager.Models;

public class HierarchicalFileEntry
{
    public string Id { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
    public string Path { get; set; }
    public string Extension { get; set; }
    public bool IsDirectory { get; set; }
    public bool HasDirectories { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateCreatedUtc { get; set; }
    public DateTime DateModified { get; set; }
    public DateTime DateModifiedUtc { get; set; }
    public List<HierarchicalFileEntry> MyDirectories { get; set; }
    public List<HierarchicalFileEntry> Items { get; set; }
}