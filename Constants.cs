namespace PokeBar;

/// <summary>
/// Constantes centralizadas do projeto para evitar magic numbers
/// </summary>
public static class Constants
{
    // === Spawn e Timers ===
    public const int MIN_SPAWN_SECONDS = 30;
    public const int MAX_SPAWN_SECONDS = 90;
    public const double BATTLE_RESOLUTION_DELAY_MS = 5500;
    public const double MANUAL_CAPTURE_TIMEOUT_SECONDS = 12;
    
    // === Física da Pokébola ===
    public const double GRAVITY_PX_PER_SEC2 = 800;
    public const double BOUNCE_DAMPING = 0.6;
    public const double AIR_RESISTANCE = 0.98;
    public const double MIN_VELOCITY_PX = 10;
    public const double MIN_DRAG_DISTANCE_PX = 20;
    
    // === Captura ===
    public const double BASE_CAPTURE_CHANCE = 0.3;
    public const double HP_FACTOR_MULTIPLIER = 0.5;
    public const double POKEBALL_COOLDOWN_SECONDS = 1.2;
    public const int ALPHA_THRESHOLD = 10; // Para detecção de sprite bounds
    
    // === Sprites ===
    public const int DEFAULT_SPRITE_WIDTH = 64;
    public const int DEFAULT_SPRITE_HEIGHT = 96;
    public const int ANIMATION_FRAME_MS = 100;
    
    // === Batalha ===
    public const int MAX_BATTLE_TURNS = 200;
    public const double DAMAGE_VARIANCE_MIN = 0.85;
    public const double DAMAGE_VARIANCE_MAX = 1.15;
    
    // === Save/Debounce ===
    public const double SAVE_DEBOUNCE_SECONDS = 2;
    public const int TASKBAR_CACHE_VALIDITY_SECONDS = 10;
    
    // === Preços Base ===
    public const int POKEBALL_PRICE = 200;
    public const int GREATBALL_PRICE = 600;
    public const int ULTRABALL_PRICE = 1200;
}
