using System.IO;
using Newtonsoft.Json;

namespace AutoRegisterLogin;

public sealed class IpWhitelist
{
    private readonly string _filePath;
    private Dictionary<string, HashSet<string>> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IpWhitelist(string filePath)
    {
        _filePath = filePath;
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            _entries = new(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var json = File.ReadAllText(_filePath);
        _entries = JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(json)
                   ?? new(StringComparer.OrdinalIgnoreCase);
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

    public bool IsWhitelisted(string playerName, string ip)
    {
        return _entries.TryGetValue(playerName, out var ips) && ips.Contains(ip);
    }

    public List<string> GetIps(string playerName)
    {
        return _entries.TryGetValue(playerName, out var ips) ? ips.ToList() : new List<string>();
    }

    public void AddIp(string playerName, string ip)
    {
        if (!_entries.TryGetValue(playerName, out var ips))
        {
            ips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _entries[playerName] = ips;
        }

        ips.Add(ip);
    }

    public void RemoveIp(string playerName, string ip)
    {
        if (_entries.TryGetValue(playerName, out var ips))
        {
            ips.Remove(ip);
            if (ips.Count == 0)
            {
                _entries.Remove(playerName);
            }
        }
    }
}
