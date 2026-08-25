using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ArcanumLib.Gui.Controls;

/// <summary>
/// Encapsulates selection state for <see cref="ArcanumList{T}" />: the currently
/// selected index and the optional callback fired when a row is activated by click.
/// </summary>
/// <typeparam name="T">The element type of the owning list.</typeparam>
internal sealed class ArcanumListSelection<T>
{
    private readonly System.Action<T, int>? _onSelected;
    private int _selectedIndex = -1;

    /// <summary>
    /// Creates a selection helper wrapping the optional activation callback.
    /// </summary>
    /// <param name="onSelected">The callback to invoke when a row is clicked, or null.</param>
    public ArcanumListSelection(System.Action<T, int>? onSelected) => _onSelected = onSelected;

    /// <summary>The zero-based index of the currently selected row, or -1 when none is selected.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Clears the selection back to "none selected".</summary>
    public void Reset() => _selectedIndex = -1;

    /// <summary>
    /// Sets the selected index without firing the activation callback. Out-of-range
    /// indices collapse to -1 (no selection), matching <see cref="ArcanumList{T}.Select" />.
    /// </summary>
    /// <param name="index">The index to select.</param>
    /// <param name="count">The current number of selectable rows.</param>
    public void Set(int index, int count)
    {
        _selectedIndex = index < 0 || index >= count ? -1 : index;
    }

    /// <summary>
    /// Selects a row as the result of a mouse click and fires the activation callback.
    /// Out-of-range clicks are ignored (no state change, no callback).
    /// </summary>
    /// <param name="index">The clicked row index.</param>
    /// <param name="items">The current row items.</param>
    /// <param name="logger">The logger used to report callback failures, or null.</param>
    public void SelectByClick(int index, IReadOnlyList<T> items, ILogger? logger)
    {
        if (index < 0 || index >= items.Count) return;

        _selectedIndex = index;
        try
        {
            _onSelected?.Invoke(items[index], index);
        }
        catch (Exception ex)
        {
            logger?.Warning("[ArcanumList] Selection callback failed: {0}", ex);
        }
    }
}
