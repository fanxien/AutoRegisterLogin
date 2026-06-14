using System.IO;
using Newtonsoft.Json;

namespace AutoRegisterLogin;

public sealed class PendingApproval
{
    public string PlayerName { get; set; } = string.Empty;
    public List<string> ExistingIps { get; set; } = new();
    public string NewIp { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PendingApprovalStore
{
    private readonly string _filePath;
    private readonly int _timeoutHours;
    private List<PendingApproval> _entries = new();

    public PendingApprovalStore(string filePath, int timeoutHours)
    {
        _filePath = filePath;
        _timeoutHours = timeoutHours;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _entries = new();
            return;
        }

        var json = File.ReadAllText(_filePath);
        _entries = JsonConvert.DeserializeObject<List<PendingApproval>>(json) ?? new();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(_filePath, JsonConvert.SerializeObject(_entries, Formatting.Indented));
    }

    public void Add(PendingApproval approval)
    {
        _entries.RemoveAll(e =>
            string.Equals(e.PlayerName, approval.PlayerName, StringComparison.OrdinalIgnoreCase));
        _entries.Add(approval);
    }

    public PendingApproval? Get(string playerName)
    {
        return _entries.Find(e =>
            string.Equals(e.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
    }

    public void Remove(string playerName)
    {
        _entries.RemoveAll(e =>
            string.Equals(e.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
    }

    public void CleanExpired()
    {
        var cutoff = DateTime.UtcNow.AddHours(-_timeoutHours);
        _entries.RemoveAll(e => e.RequestedAt < cutoff);
    }
}
