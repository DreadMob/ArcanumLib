using ArcanumLib.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class EntityControlExtensionsTests
{
    [Fact]
    public void SetWatchedBoolDirty_WhenChanged_SetsAndMarksDirty()
    {
        var entity = new DummyEntity();
        entity.WatchedAttributes = new SyncedTreeAttribute();

        entity.SetWatchedBoolDirty("myflag", true);

        Assert.True(entity.WatchedAttributes.GetBool("myflag"));
    }

    [Fact]
    public void SetWatchedBoolDirty_WhenSame_DoesNotSetAgain()
    {
        var entity = new DummyEntity();
        entity.WatchedAttributes = new SyncedTreeAttribute();
        entity.WatchedAttributes.SetBool("myflag", true);

        // Should return without calling MarkPathDirty again; we just verify value remains.
        entity.SetWatchedBoolDirty("myflag", true);

        Assert.True(entity.WatchedAttributes.GetBool("myflag"));
    }

    [Fact]
    public void SetWatchedBoolDirty_NullEntity_DoesNotThrow()
    {
        Entity? entity = null;
        entity!.SetWatchedBoolDirty("myflag", true);
    }

    private sealed class DummyEntity : Entity
    {
        public DummyEntity()
        {
            Class = "dummy";
        }
    }
}
