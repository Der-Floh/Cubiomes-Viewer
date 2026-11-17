using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Tests.Interop;

public sealed class NativeLootNamesTests
{
    [Theory]
    [InlineData(McVersion.V1_13_2)]
    [InlineData(McVersion.V26_3)]
    public void ItemName_EveryItemOfTheLootLibrary_HasAGameIdThatLeadsBackToIt(McVersion version)
    {
        for (var item = 0; item < CubiomesNative.LootItemCount(); item++)
        {
            var name = NativeLootNames.ItemName(item, version);

            Assert.NotNull(name);
            Assert.StartsWith("minecraft:", name, StringComparison.Ordinal);
            Assert.True(NativeLootNames.TryGetItem(name, out var found));
            Assert.Equal(item, found);
        }
    }

    [Fact]
    public void ItemName_ChainRenamedIn1219_KeepsOneItemUnderBothIds()
    {
        Assert.True(NativeLootNames.TryGetItem("minecraft:chain", out var chain));
        Assert.True(NativeLootNames.TryGetItem("minecraft:iron_chain", out var ironChain));

        Assert.Equal(chain, ironChain);
        Assert.Equal("minecraft:chain", NativeLootNames.ItemName(chain, McVersion.V1_21_6));
        Assert.Equal("minecraft:iron_chain", NativeLootNames.ItemName(chain, McVersion.V1_21_9));
    }

    [Fact]
    public void EnchantmentName_EveryEnchantment_HasAGameIdThatLeadsBackToIt()
    {
        for (var enchantment = 1; enchantment < CubiomesNative.LootEnchantmentCount(); enchantment++)
        {
            var name = NativeLootNames.EnchantmentName(enchantment);

            Assert.NotNull(name);
            Assert.True(NativeLootNames.TryGetEnchantment(name, out var found));
            Assert.Equal(enchantment, found);
            Assert.InRange(NativeLootNames.MaxLevel(enchantment), 1, 5);
        }
    }

    [Theory]
    [InlineData("minecraft:binding_curse")]
    [InlineData("minecraft:vanishing_curse")]
    [InlineData("minecraft:sweeping_edge")]
    [InlineData("minecraft:lunge")]
    public void TryGetEnchantment_GameId_IsKnown(string enchantment)
        => Assert.True(NativeLootNames.TryGetEnchantment(enchantment, out _));

    [Fact]
    public void EffectAndTableNames_EveryId_IsNamed()
    {
        Assert.All(Enumerable.Range(0, CubiomesNative.LootEffectCount()), effect => Assert.StartsWith("minecraft:", NativeLootNames.EffectName(effect), StringComparison.Ordinal));
        Assert.All(Enumerable.Range(0, CubiomesNative.LootTableCount()), table => Assert.NotNull(NativeLootNames.TableName(table)));
        Assert.Null(NativeLootNames.TableName(CubiomesNative.LootTableCount()));
    }
}
