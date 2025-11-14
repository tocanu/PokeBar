using Xunit;
using PokeBar.Services;
using PokeBar.Models;
using System;
using System.IO;
using System.Linq;

namespace PokeBar.Tests;

public class DexServiceTests
{
    private readonly DexService _dexService;

    public DexServiceTests()
    {
        // Create a real SpriteService with a minimal GameState
        var state = new GameState();
        var spriteService = new SpriteService(state);
        _dexService = new DexService(spriteService);
    }

    [Fact]
    public void Constructor_InitializesService()
    {
        // Assert
        Assert.NotNull(_dexService);
        Assert.NotNull(_dexService.Entries);
    }

    [Fact]
    public void Entries_ReturnsReadOnlyList()
    {
        // Act
        var entries = _dexService.Entries;

        // Assert
        Assert.NotNull(entries);
    }

    [Fact]
    public void GetEntry_WithInvalidNumber_ReturnsNull()
    {
        // Act
        var entry = _dexService.GetEntry(9999);

        // Assert
        Assert.Null(entry);
    }

    [Fact]
    public void GetEntry_WithNegativeNumber_ReturnsNull()
    {
        // Act
        var entry = _dexService.GetEntry(-1);

        // Assert
        Assert.Null(entry);
    }

    [Fact]
    public void GetEntry_WithZero_ReturnsNull()
    {
        // Act
        var entry = _dexService.GetEntry(0);

        // Assert
        Assert.Null(entry);
    }

    [Fact]
    public void CreateRandomPokemon_ReturnsValidPokemon()
    {
        // Arrange
        var rng = new Random(42); // Fixed seed for consistency

        // Act
        var pokemon = _dexService.CreateRandomPokemon(rng);

        // Assert
        Assert.NotNull(pokemon);
        Assert.True(pokemon.Level > 0, "Level should be positive");
        Assert.True(pokemon.MaxHP > 0, "MaxHP should be positive");
        Assert.True(pokemon.Attack > 0, "Attack should be positive");
        Assert.True(pokemon.Defense > 0, "Defense should be positive");
        Assert.True(pokemon.Speed > 0, "Speed should be positive");
    }

    [Fact]
    public void CreatePokemon_WithValidEntry_CreatesValidPokemon()
    {
        // Arrange
        var entry = new DexEntry(1, "Bulbasaur", "0001");
        var rng = new Random(42); // Fixed seed for consistency

        // Act
        var pokemon = _dexService.CreatePokemon(entry, rng);

        // Assert
        Assert.NotNull(pokemon);
        Assert.Equal("Bulbasaur", pokemon.Name);
        Assert.Equal(1, pokemon.DexNumber);
        Assert.True(pokemon.Level >= 5 && pokemon.Level <= 30, "Level should be between 5 and 30");
        Assert.True(pokemon.MaxHP >= 20 && pokemon.MaxHP <= 180, "HP should be between 20 and 180");
        Assert.True(pokemon.Attack >= 8 && pokemon.Attack <= 200, "Attack should be between 8 and 200");
        Assert.True(pokemon.Defense >= 6 && pokemon.Defense <= 200, "Defense should be between 6 and 200");
        Assert.True(pokemon.Speed >= 6 && pokemon.Speed <= 200, "Speed should be between 6 and 200");
        Assert.Equal(pokemon.MaxHP, pokemon.CurrentHP); // Starts at full HP
    }

    [Fact]
    public void CreatePokemon_WithSameSeed_CreatesConsistentPokemon()
    {
        // Arrange
        var entry = new DexEntry(25, "Pikachu", "0025");
        var rng1 = new Random(12345);
        var rng2 = new Random(12345);

        // Act
        var pokemon1 = _dexService.CreatePokemon(entry, rng1);
        var pokemon2 = _dexService.CreatePokemon(entry, rng2);

        // Assert
        Assert.Equal(pokemon1.Level, pokemon2.Level);
        Assert.Equal(pokemon1.MaxHP, pokemon2.MaxHP);
        Assert.Equal(pokemon1.Attack, pokemon2.Attack);
        Assert.Equal(pokemon1.Defense, pokemon2.Defense);
        Assert.Equal(pokemon1.Speed, pokemon2.Speed);
    }

    [Theory]
    [InlineData(1, "Bulbasaur", "0001")]
    [InlineData(25, "Pikachu", "0025")]
    [InlineData(150, "Mewtwo", "0150")]
    public void CreatePokemon_WithDifferentEntries_CreatesUniquePokemon(int dexNumber, string name, string folderName)
    {
        // Arrange
        var entry = new DexEntry(dexNumber, name, folderName);
        var rng = new Random(42);

        // Act
        var pokemon = _dexService.CreatePokemon(entry, rng);

        // Assert
        Assert.Equal(name, pokemon.Name);
        Assert.Equal(dexNumber, pokemon.DexNumber);
        Assert.True(pokemon.MaxHP > 0);
        Assert.True(pokemon.Attack > 0);
        Assert.True(pokemon.Defense > 0);
        Assert.True(pokemon.Speed > 0);
    }

    [Fact]
    public void CreatePokemon_StatsAreClamped()
    {
        // Arrange - High dex number to test clamping
        var entry = new DexEntry(999, "TestMon", "0999");
        var rng = new Random();

        // Act
        var pokemon = _dexService.CreatePokemon(entry, rng);

        // Assert - All stats should be within valid ranges
        Assert.InRange(pokemon.MaxHP, 20, 180);
        Assert.InRange(pokemon.Attack, 8, 200);
        Assert.InRange(pokemon.Defense, 6, 200);
        Assert.InRange(pokemon.Speed, 6, 200);
    }
}
