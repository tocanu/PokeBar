using System.Collections.Generic;
using System.Linq;

namespace PokeBar.Models;

public class BallDefinition
{
    public BallType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Price { get; init; }
    public double CatchRateMultiplier { get; init; }
    public string Icon { get; init; } = "⚪";

    private static readonly Dictionary<BallType, BallDefinition> _definitions = new()
    {
        [BallType.PokeBall] = new() 
        { 
            Type = BallType.PokeBall,
            Name = "Poké Ball",
            Price = 200,
            CatchRateMultiplier = 1.0,
            Icon = "⚪"
        },
        [BallType.GreatBall] = new() 
        { 
            Type = BallType.GreatBall,
            Name = "Great Ball",
            Price = 600,
            CatchRateMultiplier = 1.5,
            Icon = "🔵"
        },
        [BallType.UltraBall] = new() 
        { 
            Type = BallType.UltraBall,
            Name = "Ultra Ball",
            Price = 1200,
            CatchRateMultiplier = 2.0,
            Icon = "⚫"
        },
        [BallType.MasterBall] = new()
        {
            Type = BallType.MasterBall,
            Name = "Master Ball",
            Price = 0,
            CatchRateMultiplier = 255.0,
            Icon = "💜"
        },
        [BallType.SafariBall] = new()
        {
            Type = BallType.SafariBall,
            Name = "Safari Ball",
            Price = 0,
            CatchRateMultiplier = 1.5,
            Icon = "🟢"
        },
        [BallType.NetBall] = new() 
        { 
            Type = BallType.NetBall,
            Name = "Net Ball",
            Price = 1000,
            CatchRateMultiplier = 3.0,
            Icon = "🌊"
        },
        [BallType.DiveBall] = new()
        {
            Type = BallType.DiveBall,
            Name = "Dive Ball",
            Price = 1000,
            CatchRateMultiplier = 3.5,
            Icon = "💧"
        },
        [BallType.NestBall] = new()
        {
            Type = BallType.NestBall,
            Name = "Nest Ball",
            Price = 1000,
            CatchRateMultiplier = 1.0,
            Icon = "🟡"
        },
        [BallType.RepeatBall] = new()
        {
            Type = BallType.RepeatBall,
            Name = "Repeat Ball",
            Price = 1000,
            CatchRateMultiplier = 3.0,
            Icon = "🔁"
        },
        [BallType.TimerBall] = new()
        {
            Type = BallType.TimerBall,
            Name = "Timer Ball",
            Price = 1000,
            CatchRateMultiplier = 1.0,
            Icon = "⏱️"
        },
        [BallType.LuxuryBall] = new()
        {
            Type = BallType.LuxuryBall,
            Name = "Luxury Ball",
            Price = 1000,
            CatchRateMultiplier = 1.0,
            Icon = "✨"
        },
        [BallType.PremierBall] = new()
        {
            Type = BallType.PremierBall,
            Name = "Premier Ball",
            Price = 200,
            CatchRateMultiplier = 1.0,
            Icon = "⚪"
        },
        [BallType.DuskBall] = new()
        {
            Type = BallType.DuskBall,
            Name = "Dusk Ball",
            Price = 1000,
            CatchRateMultiplier = 3.0,
            Icon = "🌙"
        },
        [BallType.HealBall] = new()
        {
            Type = BallType.HealBall,
            Name = "Heal Ball",
            Price = 300,
            CatchRateMultiplier = 1.0,
            Icon = "💚"
        },
        [BallType.QuickBall] = new() 
        { 
            Type = BallType.QuickBall,
            Name = "Quick Ball",
            Price = 1000,
            CatchRateMultiplier = 5.0,
            Icon = "⚡"
        },
        [BallType.CherishBall] = new()
        {
            Type = BallType.CherishBall,
            Name = "Cherish Ball",
            Price = 0,
            CatchRateMultiplier = 1.0,
            Icon = "❤️"
        },
        [BallType.FastBall] = new()
        {
            Type = BallType.FastBall,
            Name = "Fast Ball",
            Price = 300,
            CatchRateMultiplier = 4.0,
            Icon = "💨"
        },
        [BallType.LevelBall] = new()
        {
            Type = BallType.LevelBall,
            Name = "Level Ball",
            Price = 300,
            CatchRateMultiplier = 1.0,
            Icon = "📊"
        },
        [BallType.LureBall] = new()
        {
            Type = BallType.LureBall,
            Name = "Lure Ball",
            Price = 300,
            CatchRateMultiplier = 4.0,
            Icon = "🎣"
        },
        [BallType.HeavyBall] = new()
        {
            Type = BallType.HeavyBall,
            Name = "Heavy Ball",
            Price = 300,
            CatchRateMultiplier = 1.0,
            Icon = "⚫"
        },
        [BallType.LoveBall] = new()
        {
            Type = BallType.LoveBall,
            Name = "Love Ball",
            Price = 300,
            CatchRateMultiplier = 8.0,
            Icon = "💗"
        },
        [BallType.FriendBall] = new()
        {
            Type = BallType.FriendBall,
            Name = "Friend Ball",
            Price = 300,
            CatchRateMultiplier = 1.0,
            Icon = "💛"
        },
        [BallType.MoonBall] = new()
        {
            Type = BallType.MoonBall,
            Name = "Moon Ball",
            Price = 300,
            CatchRateMultiplier = 4.0,
            Icon = "🌕"
        },
        [BallType.SportBall] = new()
        {
            Type = BallType.SportBall,
            Name = "Sport Ball",
            Price = 300,
            CatchRateMultiplier = 1.5,
            Icon = "⚾"
        },
        [BallType.ParkBall] = new()
        {
            Type = BallType.ParkBall,
            Name = "Park Ball",
            Price = 0,
            CatchRateMultiplier = 255.0,
            Icon = "🏞️"
        },
        [BallType.DreamBall] = new()
        {
            Type = BallType.DreamBall,
            Name = "Dream Ball",
            Price = 0,
            CatchRateMultiplier = 4.0,
            Icon = "💤"
        }
    };

    public static BallDefinition Get(BallType type) => _definitions[type];
    public static bool TryGet(BallType type, out BallDefinition? definition) => _definitions.TryGetValue(type, out definition);
    public static IEnumerable<BallDefinition> GetAll() => _definitions.Values;
    public static IEnumerable<BallDefinition> GetPurchasable() => _definitions.Values.Where(b => b.Price > 0);
}
