using Xunit;
using PokeBar.Services;
using PokeBar.Models;
using System;

namespace PokeBar.Tests;

public class BattleServiceTests
{
    private readonly GameState _state;
    private readonly DexService _dexService;
    private readonly BattleService _battleService;

    public BattleServiceTests()
    {
        _state = new GameState();
        var spriteService = new SpriteService(_state);
        _dexService = new DexService(spriteService);
        _battleService = new BattleService(_state, _dexService);
    }

    [Fact]
    public void Constructor_InitializesService()
    {
        // Assert
        Assert.NotNull(_battleService);
    }

    [Fact]
    public void ForceSpawn_DoesNotThrowException()
    {
        // Act
        var exception = Record.Exception(() => _battleService.ForceSpawn());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void TryManualCapture_WithoutActiveWild_ReturnsFalse()
    {
        // Act
        var result = _battleService.TryManualCapture();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void BattleStarted_EventCanBeSubscribed()
    {
        // Arrange
        bool eventRaised = false;
        _battleService.BattleStarted += (sender, pokemon) => { eventRaised = true; };

        // Act - We can only verify subscription works, event triggering requires internal state
        
        // Assert
        Assert.False(eventRaised); // Event not raised yet
    }

    [Fact]
    public void BattleFinished_EventCanBeSubscribed()
    {
        // Arrange
        bool eventRaised = false;
        _battleService.BattleFinished += (sender, args) => { eventRaised = true; };

        // Act - We can only verify subscription works
        
        // Assert
        Assert.False(eventRaised); // Event not raised yet
    }

    [Fact]
    public void Notify_EventCanBeSubscribed()
    {
        // Arrange
        bool eventRaised = false;
        _battleService.Notify += (sender, message) => { eventRaised = true; };

        // Act - We can only verify subscription works
        
        // Assert
        Assert.False(eventRaised); // Event not raised yet
    }
}
