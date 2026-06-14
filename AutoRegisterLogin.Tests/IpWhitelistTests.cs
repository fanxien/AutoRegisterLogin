using Xunit;

namespace AutoRegisterLogin.Tests;

public class IpWhitelistTests
{
    [Fact]
    public void IsWhitelisted_EmptyStore_ReturnsFalse()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            Assert.False(wl.IsWhitelisted("Player1", "192.168.1.1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddIp_ThenIsWhitelisted_ReturnsTrue()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            Assert.True(wl.IsWhitelisted("Player1", "192.168.1.1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddMultipleIps_AllPresentInGetIps()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            wl.AddIp("Player1", "10.0.0.1");
            var ips = wl.GetIps("Player1");
            Assert.Contains("192.168.1.1", ips);
            Assert.Contains("10.0.0.1", ips);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RemoveIp_ThenIsWhitelisted_ReturnsFalse()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            wl.RemoveIp("Player1", "192.168.1.1");
            Assert.False(wl.IsWhitelisted("Player1", "192.168.1.1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RemoveLastIp_GetIps_ReturnsEmpty()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            wl.AddIp("Player1", "192.168.1.1");
            wl.RemoveIp("Player1", "192.168.1.1");
            Assert.Empty(wl.GetIps("Player1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveAndLoad_PreservesData()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl1 = new IpWhitelist(path);
            wl1.Load();
            wl1.AddIp("Player1", "192.168.1.1");
            wl1.AddIp("Player2", "10.0.0.1");
            wl1.Save();

            var wl2 = new IpWhitelist(path);
            wl2.Load();
            Assert.True(wl2.IsWhitelisted("Player1", "192.168.1.1"));
            Assert.True(wl2.IsWhitelisted("Player2", "10.0.0.1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void GetIps_NonexistentPlayer_ReturnsEmptyList()
    {
        var path = Path.GetTempFileName();
        try
        {
            var wl = new IpWhitelist(path);
            wl.Load();
            Assert.Empty(wl.GetIps("Nobody"));
        }
        finally { File.Delete(path); }
    }
}
