using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace ArcanumLib.Inventory;

/// <summary>
/// Fluent builder for constructing <see cref="ItemStack" /> instances with
/// attributes, durability, stack size, and custom collectible codes. Useful for
/// loot tables, rewards, and test fixtures.
/// </summary>
public sealed class ItemStackBuilder
{
    private AssetLocation? _code;
    private int _stackSize = 1;
    private int? _durability;
    private readonly TreeAttribute _attributes = new();
    private readonly Dictionary<string, IAttribute> _watchedAttributes = new(StringComparer.OrdinalIgnoreCase);
    private EnumItemClass? _itemClass;

    /// <summary>
    /// Creates an empty builder.
    /// </summary>
    public ItemStackBuilder() { }

    /// <summary>
    /// Creates a builder seeded from an existing stack. The original stack is not modified.
    /// </summary>
    /// <param name="source">The source value.</param>
    public ItemStackBuilder(ItemStack? source)
    {
        if (source == null) return;
        _code = source.Collectible?.Code;
        _stackSize = source.StackSize;
        _durability = source.Attributes?.GetInt("durability");
        _itemClass = source.Class;
        if (source.Attributes != null)
            _attributes = source.Attributes.Clone() as TreeAttribute ?? new TreeAttribute();
    }

    /// <summary>
    /// Sets the collectible code (e.g. <c>"game:ingot-iron"</c>).
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The code.</returns>
    public ItemStackBuilder Code(string code)
    {
        _code = AssetLocation.CreateOrNull(code);
        return this;
    }

    /// <summary>
    /// Sets the collectible code.
    /// </summary>
    /// <param name="code">The code value.</param>
    /// <returns>The code.</returns>
    public ItemStackBuilder Code(AssetLocation code)
    {
        _code = code;
        return this;
    }

    /// <summary>
    /// Sets the stack size.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>The count.</returns>
    public ItemStackBuilder Count(int size)
    {
        _stackSize = Math.Max(1, size);
        return this;
    }

    /// <summary>
    /// Sets the durability attribute.
    /// </summary>
    /// <param name="durability">The durability value.</param>
    /// <returns>The durability.</returns>
    public ItemStackBuilder Durability(int durability)
    {
        _durability = durability;
        return this;
    }

    /// <summary>
    /// Sets the item class (Item or Block).
    /// </summary>
    /// <param name="itemClass">The item class value.</param>
    /// <returns>The item class.</returns>
    public ItemStackBuilder ItemClass(EnumItemClass itemClass)
    {
        _itemClass = itemClass;
        return this;
    }

    /// <summary>
    /// Sets a string attribute.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The attribute.</returns>
    public ItemStackBuilder Attribute(string key, string value)
    {
        _attributes.SetString(key, value);
        return this;
    }

    /// <summary>
    /// Sets an integer attribute.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The attribute.</returns>
    public ItemStackBuilder Attribute(string key, int value)
    {
        _attributes.SetInt(key, value);
        return this;
    }

    /// <summary>
    /// Sets a float attribute.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The attribute.</returns>
    public ItemStackBuilder Attribute(string key, float value)
    {
        _attributes.SetFloat(key, value);
        return this;
    }

    /// <summary>
    /// Sets a boolean attribute.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The attribute.</returns>
    public ItemStackBuilder Attribute(string key, bool value)
    {
        _attributes.SetBool(key, value);
        return this;
    }

    /// <summary>
    /// Sets a generic attribute value.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The attribute.</returns>
    public ItemStackBuilder Attribute(string key, IAttribute value)
    {
        _attributes[key] = value;
        return this;
    }

    /// <summary>
    /// Sets a watched attribute (synced to clients).
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The watched attribute.</returns>
    public ItemStackBuilder WatchedAttribute(string key, IAttribute value)
    {
        _watchedAttributes[key] = value;
        return this;
    }

    /// <summary>
    /// Sets a watched string attribute.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The watched attribute.</returns>
    public ItemStackBuilder WatchedAttribute(string key, string value)
    {
        _watchedAttributes[key] = new StringAttribute(value);
        return this;
    }

    /// <summary>
    /// Sets a watched integer attribute.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value to set or compare.</param>
    /// <returns>The watched attribute.</returns>
    public ItemStackBuilder WatchedAttribute(string key, int value)
    {
        _watchedAttributes[key] = new IntAttribute(value);
        return this;
    }

    /// <summary>
    /// Removes an attribute if present.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The remove attribute.</returns>
    public ItemStackBuilder RemoveAttribute(string key)
    {
        _attributes.RemoveAttribute(key);
        return this;
    }

    /// <summary>
    /// Builds the <see cref="ItemStack" /> using the configured values.
    /// Returns null if no code was set or the collectible is not found.
    /// </summary>
    /// <param name="api">The core API for registry lookups.</param>
    /// <returns>The value, or null if none is found.</returns>
    public ItemStack? Build(ICoreAPI api)
    {
        if (api == null || _code == null) return null;

        CollectibleObject? collectible = _itemClass == EnumItemClass.Block
            ? (CollectibleObject?)api.World.GetBlock(_code)
            : _itemClass == EnumItemClass.Item
                ? api.World.GetItem(_code)
                : (CollectibleObject?)api.World.GetItem(_code) ?? api.World.GetBlock(_code);

        if (collectible == null) return null;

        var stack = new ItemStack(collectible)
        {
            StackSize = _stackSize
        };

        if (_attributes.Count > 0)
        {
            stack.Attributes ??= new TreeAttribute();
            foreach (var attr in _attributes)
                stack.Attributes[attr.Key] = attr.Value;
        }

        if (_durability.HasValue)
        {
            stack.Attributes ??= new TreeAttribute();
            stack.Attributes.SetInt("durability", _durability.Value);
        }

        if (_watchedAttributes.Count > 0)
        {
            stack.Attributes ??= new TreeAttribute();
            foreach (var kvp in _watchedAttributes)
                stack.Attributes[kvp.Key] = kvp.Value;
        }

        return stack;
    }

    /// <summary>
    /// Builds the stack and returns it, throwing if the collectible is not found.
    /// </summary>
    /// <param name="api">The core API instance.</param>
    /// <returns>The or throw.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the operation is invalid for the current state.</exception>
    public ItemStack BuildOrThrow(ICoreAPI api)
    {
        return Build(api)
            ?? throw new InvalidOperationException($"Collectible '{_code}' not found in registry.");
    }

    /// <summary>
    /// Resets the builder to an empty state.
    /// </summary>
    /// <returns>The clear.</returns>
    public ItemStackBuilder Clear()
    {
        _code = null;
        _stackSize = 1;
        _durability = null;
        _attributes.Clear();
        _watchedAttributes.Clear();
        _itemClass = null;
        return this;
    }
}
