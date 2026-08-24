using System.Threading;
using ArcanumLib.Caching;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class TimedCacheTests
{
    [Fact]
    public void Add_And_TryGetValue_RetrievesValue()
    {
        using var cache = new TimedCache<string, int>(null, 10000);
        cache.Add("key", 42);

        Assert.True(cache.TryGetValue("key", out var value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryGetValue_MissingKey_ReturnsFalse()
    {
        using var cache = new TimedCache<string, int>(null, 10000);

        Assert.False(cache.TryGetValue("missing", out _));
    }

    [Fact]
    public void Expired_Entry_Is_Removed()
    {
        using var cache = new TimedCache<string, int>(null, 50);
        cache.Add("key", 42);

        Thread.Sleep(80);

        Assert.False(cache.TryGetValue("key", out _));
    }

    [Fact]
    public void Clean_RemovesStaleEntries()
    {
        using var cache = new TimedCache<string, int>(null, 50);
        cache.Add("stale", 1);
        cache.Add("fresh", 2);

        Thread.Sleep(80);
        cache.Add("fresh", 2);
        cache.Clean();

        Assert.False(cache.TryGetValue("stale", out _));
        Assert.True(cache.TryGetValue("fresh", out _));
    }

    [Fact]
    public void MaxSize_Trims_Oldest()
    {
        using var cache = new TimedCache<string, int>(null, 10000, maxSize: 2);
        cache.Add("first", 1);
        Thread.Sleep(20);
        cache.Add("second", 2);
        Thread.Sleep(20);
        cache.Add("third", 3);

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGetValue("first", out _));
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        using var cache = new TimedCache<string, int>(null, 10000);
        cache.Add("key", 42);

        Assert.True(cache.Remove("key"));
        Assert.False(cache.TryGetValue("key", out _));
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        using var cache = new TimedCache<string, int>(null, 10000);
        cache.Add("a", 1);
        cache.Add("b", 2);

        cache.Clear();

        Assert.Equal(0, cache.Count);
    }
}
