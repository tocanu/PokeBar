namespace PokeBar.Models;

public class Pokemon
{
    public string Name { get; set; } = "Slakoth";
    public int DexNumber { get; set; } = 287;
    public int Level { get; set; } = 5;
    public int MaxHP { get; set; } = 20;
    public int CurrentHP { get; set; } = 20;
    public int Attack { get; set; } = 10;
    public int Defense { get; set; } = 10;
    public int Speed { get; set; } = 10;

    public Pokemon Clone() => new()
    {
        Name = Name,
        DexNumber = DexNumber,
        Level = Level,
        MaxHP = MaxHP,
        CurrentHP = CurrentHP,
        Attack = Attack,
        Defense = Defense,
        Speed = Speed,
    };

    public void HealFull() => CurrentHP = MaxHP;
}
