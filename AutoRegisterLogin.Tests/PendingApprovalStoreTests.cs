using Xunit;

namespace AutoRegisterLogin.Tests;

public class PendingApprovalStoreTests
{
    private const int TimeoutHours = 72;

    [Fact]
    public void Get_NonexistentPlayer_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            Assert.Null(store.Get("Nobody"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Add_ThenGet_ReturnsApproval()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                ExistingIps = new List<string> { "192.168.1.1" },
                NewIp = "10.0.0.1",
                RequestedAt = DateTime.UtcNow
            });

            var retrieved = store.Get("Player1");
            Assert.NotNull(retrieved);
            Assert.Equal("10.0.0.1", retrieved!.NewIp);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Add_DuplicatePlayer_ReplacesEntry()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string> { "192.168.1.1" }
            });
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.2",
                ExistingIps = new List<string> { "192.168.1.1" }
            });

            Assert.Equal("10.0.0.2", store.Get("Player1")!.NewIp);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Remove_ThenGet_ReturnsNull()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, TimeoutHours);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string>()
            });
            store.Remove("Player1");
            Assert.Null(store.Get("Player1"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void CleanExpired_RemovesOldEntries_KeepsNewOnes()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store = new PendingApprovalStore(path, 1);
            store.Load();
            store.Add(new PendingApproval
            {
                PlayerName = "OldPlayer",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string>(),
                RequestedAt = DateTime.UtcNow.AddHours(-2)
            });
            store.Add(new PendingApproval
            {
                PlayerName = "NewPlayer",
                NewIp = "10.0.0.2",
                ExistingIps = new List<string>(),
                RequestedAt = DateTime.UtcNow
            });

            store.CleanExpired();
            Assert.Null(store.Get("OldPlayer"));
            Assert.NotNull(store.Get("NewPlayer"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveAndLoad_PreservesData()
    {
        var path = Path.GetTempFileName();
        try
        {
            var store1 = new PendingApprovalStore(path, TimeoutHours);
            store1.Load();
            store1.Add(new PendingApproval
            {
                PlayerName = "Player1",
                NewIp = "10.0.0.1",
                ExistingIps = new List<string> { "192.168.1.1" },
                RequestedAt = DateTime.UtcNow
            });
            store1.Save();

            var store2 = new PendingApprovalStore(path, TimeoutHours);
            store2.Load();
            var retrieved = store2.Get("Player1");
            Assert.NotNull(retrieved);
            Assert.Equal("10.0.0.1", retrieved!.NewIp);
        }
        finally { File.Delete(path); }
    }
}
