namespace PokeBar.Models
{
    public enum BallType
    {
        PokeBall,      // 1x taxa base
        GreatBall,     // 1.5x
        UltraBall,     // 2x
        MasterBall,    // 100% captura
        SafariBall,    // 1.5x (apenas Safari)
        NetBall,       // 3x contra água/inseto
        DiveBall,      // 3.5x debaixo d'água
        NestBall,      // Melhor contra baixo nível
        RepeatBall,    // 3x se já capturou
        TimerBall,     // Aumenta com turnos
        LuxuryBall,    // 1x mas aumenta felicidade
        PremierBall,   // 1x (bônus)
        DuskBall,      // 3x à noite/caverna
        HealBall,      // 1x mas cura
        QuickBall,     // 5x no primeiro turno
        CherishBall,   // 1x (evento)
        FastBall,      // 4x contra rápidos
        LevelBall,     // Baseado em nível
        LureBall,      // 4x contra pescados
        HeavyBall,     // Melhor contra pesados
        LoveBall,      // 8x se mesmo gênero
        FriendBall,    // 1x mas mais amizade
        MoonBall,      // 4x contra Pedra Lunar
        SportBall,     // 1.5x (Bug Contest)
        ParkBall,      // 100% (Pal Park)
        DreamBall      // 4x contra dormindo
    }

    public static class BallInfo
    {
        public static string GetName(BallType type) => type switch
        {
            BallType.PokeBall => "Poké Ball",
            BallType.GreatBall => "Great Ball",
            BallType.UltraBall => "Ultra Ball",
            BallType.MasterBall => "Master Ball",
            BallType.SafariBall => "Safari Ball",
            BallType.NetBall => "Net Ball",
            BallType.DiveBall => "Dive Ball",
            BallType.NestBall => "Nest Ball",
            BallType.RepeatBall => "Repeat Ball",
            BallType.TimerBall => "Timer Ball",
            BallType.LuxuryBall => "Luxury Ball",
            BallType.PremierBall => "Premier Ball",
            BallType.DuskBall => "Dusk Ball",
            BallType.HealBall => "Heal Ball",
            BallType.QuickBall => "Quick Ball",
            BallType.CherishBall => "Cherish Ball",
            BallType.FastBall => "Fast Ball",
            BallType.LevelBall => "Level Ball",
            BallType.LureBall => "Lure Ball",
            BallType.HeavyBall => "Heavy Ball",
            BallType.LoveBall => "Love Ball",
            BallType.FriendBall => "Friend Ball",
            BallType.MoonBall => "Moon Ball",
            BallType.SportBall => "Sport Ball",
            BallType.ParkBall => "Park Ball",
            BallType.DreamBall => "Dream Ball",
            _ => "Unknown"
        };

        public static int GetPrice(BallType type) => type switch
        {
            BallType.PokeBall => 200,
            BallType.GreatBall => 600,
            BallType.UltraBall => 1200,
            BallType.MasterBall => 0, // Não pode comprar
            BallType.SafariBall => 0, // Não pode comprar
            BallType.NetBall => 1000,
            BallType.DiveBall => 1000,
            BallType.NestBall => 1000,
            BallType.RepeatBall => 1000,
            BallType.TimerBall => 1000,
            BallType.LuxuryBall => 1000,
            BallType.PremierBall => 200,
            BallType.DuskBall => 1000,
            BallType.HealBall => 300,
            BallType.QuickBall => 1000,
            BallType.CherishBall => 0, // Não pode comprar
            BallType.FastBall => 300,
            BallType.LevelBall => 300,
            BallType.LureBall => 300,
            BallType.HeavyBall => 300,
            BallType.LoveBall => 300,
            BallType.FriendBall => 300,
            BallType.MoonBall => 300,
            BallType.SportBall => 300,
            BallType.ParkBall => 0, // Não pode comprar
            BallType.DreamBall => 0, // Não pode comprar
            _ => 0
        };

        public static double GetBaseCatchRate(BallType type) => type switch
        {
            BallType.PokeBall => 1.0,
            BallType.GreatBall => 1.5,
            BallType.UltraBall => 2.0,
            BallType.MasterBall => 255.0, // Sempre captura
            BallType.SafariBall => 1.5,
            BallType.NetBall => 1.0, // 3.0 contra água/inseto (implementar condição)
            BallType.DiveBall => 1.0, // 3.5 debaixo d'água (implementar condição)
            BallType.NestBall => 1.0, // Varia com nível
            BallType.RepeatBall => 1.0, // 3.0 se já capturado
            BallType.TimerBall => 1.0, // Aumenta com turnos
            BallType.LuxuryBall => 1.0,
            BallType.PremierBall => 1.0,
            BallType.DuskBall => 1.0, // 3.0 à noite/caverna
            BallType.HealBall => 1.0,
            BallType.QuickBall => 1.0, // 5.0 primeiro turno
            BallType.CherishBall => 1.0,
            BallType.FastBall => 1.0, // 4.0 contra rápidos
            BallType.LevelBall => 1.0, // Varia com diferença de nível
            BallType.LureBall => 1.0, // 4.0 contra pescados
            BallType.HeavyBall => 1.0, // Varia com peso
            BallType.LoveBall => 1.0, // 8.0 se mesmo gênero
            BallType.FriendBall => 1.0,
            BallType.MoonBall => 1.0, // 4.0 contra Pedra Lunar
            BallType.SportBall => 1.5,
            BallType.ParkBall => 255.0, // Sempre captura
            BallType.DreamBall => 1.0, // 4.0 contra dormindo
            _ => 1.0
        };
    }
}
