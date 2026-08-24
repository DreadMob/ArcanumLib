using ArcanumLib.Items;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace ArcanumLib.Tests.Unit;

public class ItemChargeTests
{
    private const string GenericChargeKey = "arcanumlib:attr:charge";
    private const string TimeChargeKey = "arcanumlib:attr:chargehours";

    [Fact]
    public void FindChargeKey_PrefersGenericThenSuffixed()
    {
        var stack = NewStack();

        stack.Attributes.SetFloat(TimeChargeKey, 5f);
        Assert.Equal(TimeChargeKey, ItemCharge.FindChargeKey(stack));

        stack.Attributes.SetFloat(GenericChargeKey, 10f);
        Assert.Equal(GenericChargeKey, ItemCharge.FindChargeKey(stack));
    }

    [Fact]
    public void GetSetChargeValue_ClampToMax()
    {
        var stack = NewStack();

        stack.Attributes.SetFloat(GenericChargeKey, 50f);
        stack.Attributes.SetFloat("arcanumlib:chargemax", 100f);

        Assert.Equal(50f, ItemCharge.GetChargeValue(stack));

        ItemCharge.SetChargeValue(stack, 200f);
        Assert.Equal(100f, ItemCharge.GetChargeValue(stack));

        ItemCharge.SetChargeValue(stack, -10f);
        Assert.Equal(0f, ItemCharge.GetChargeValue(stack));
    }

    [Fact]
    public void GetChargePercentage_CalculatesCorrectly()
    {
        var stack = NewStack();

        stack.Attributes.SetFloat(GenericChargeKey, 25f);
        stack.Attributes.SetFloat("arcanumlib:chargemax", 100f);

        Assert.Equal(25f, ItemCharge.GetChargePercentage(stack), 5);
    }

    [Fact]
    public void TryRecharge_IncreasesChargeByPerUnit()
    {
        var stack = NewStack();

        stack.Attributes.SetFloat(GenericChargeKey, 40f);
        stack.Attributes.SetFloat("arcanumlib:chargemax", 100f);
        stack.Attributes.SetFloat("arcanumlib:chargeperunit", 15f);

        Assert.True(ItemCharge.TryRecharge(stack, out int consumed));
        Assert.Equal(1, consumed);
        Assert.Equal(55f, ItemCharge.GetChargeValue(stack), 5);
    }

    [Fact]
    public void TryRecharge_StopsAtMax()
    {
        var stack = NewStack();

        stack.Attributes.SetFloat(GenericChargeKey, 95f);
        stack.Attributes.SetFloat("arcanumlib:chargemax", 100f);
        stack.Attributes.SetFloat("arcanumlib:chargeperunit", 20f);

        Assert.True(ItemCharge.TryRecharge(stack, out _));
        Assert.Equal(100f, ItemCharge.GetChargeValue(stack), 5);
    }

    [Fact]
    public void TryConsumeCharge_ReducesCharge()
    {
        var stack = NewStack();

        stack.Attributes.SetFloat(GenericChargeKey, 50f);

        Assert.True(ItemCharge.TryConsumeCharge(stack, 20f));
        Assert.Equal(30f, ItemCharge.GetChargeValue(stack), 5);
    }

    [Fact]
    public void TryDrainTimeCharge_ReducesHours()
    {
        var stack = NewStack();

        stack.Attributes.SetFloat(TimeChargeKey, 10f);

        Assert.True(ItemCharge.TryDrainTimeCharge(stack, 3f));
        Assert.Equal(7f, ItemCharge.GetChargeValue(stack), 5);
    }

    [Fact]
    public void GetChargeUnit_TimeSuffix_ReturnsH()
    {
        Assert.Equal("h", ItemCharge.GetChargeUnit("chargehours"));
    }

    [Fact]
    public void GetChargeUnit_Unknown_ReturnsEmpty()
    {
        Assert.Equal("", ItemCharge.GetChargeUnit("chargeuses"));
    }

    private static ItemStack NewStack()
    {
        return new ItemStack(new DummyCollectible())
        {
            Attributes = new TreeAttribute()
        };
    }

    private class DummyCollectible : CollectibleObject
    {
        public override EnumItemClass ItemClass => EnumItemClass.Item;
        public override int Id => 0;
    }
}
