using ArcanumLib.Rendering;
using Vintagestory.Client.NoObf;

namespace ArcanumLib.Tests.Unit;

/// <summary>Textures with a side of 4 mod 8 must not push the textures packed after them off the 8 px mip grid (<see cref="AtlasMipGridPatch" />).</summary>
public class AtlasMipGridPatchTests : IDisposable
{
    public AtlasMipGridPatchTests() => AtlasMipGridPatch.Apply();
    public void Dispose() => AtlasMipGridPatch.Dispose();

    private static int[] Solid(int w, int h, int color) => Enumerable.Repeat(color, w * h).ToArray();

    [Fact]
    public void Texture_After_An_Odd_One_Starts_On_The_Grid()
    {
        var atlas = new TextureAtlas(256, 256, 0, 0);
        var odd = atlas.AllocateTextureSpace(1, 84, 84);
        var next = atlas.AllocateTextureSpace(2, 32, 32);

        Assert.Equal(84, (int)Math.Round((odd.x2 - odd.x1) * 256));
        Assert.Equal(84, (int)Math.Round((odd.y2 - odd.y1) * 256));
        Assert.Equal(0, (int)Math.Round(next.x1 * 256) % 8);
        Assert.Equal(0, (int)Math.Round(next.y1 * 256) % 8);
    }

    [Fact]
    public void Spare_Strip_Repeats_The_Edge()
    {
        var atlas = new TextureAtlas(64, 64, 0, 0);
        var pixels = Solid(12, 12, 0x112233);
        for (int y = 0; y < 12; y++) pixels[y * 12 + 11] = 0x445566;   // right column
        Assert.True(atlas.InsertTexture(1, 12, 12, pixels));

        var atlasPixels = HarmonyLib.Traverse.Create(atlas).Field<int[]>("atlasPixels").Value;
        Assert.Equal(0x445566, atlasPixels[0 * 64 + 15]);    // right of the texture
        Assert.Equal(0x112233, atlasPixels[15 * 64 + 0]);    // below it
        Assert.Equal(0x445566, atlasPixels[15 * 64 + 15]);   // corner
        Assert.Equal(0, atlasPixels[0 * 64 + 16]);           // next cell untouched
    }
}
