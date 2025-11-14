using System.Collections.Generic;
using System.Windows.Media;

namespace PokeBar.Models;

public record SpriteAnimationSet(
    IReadOnlyList<ImageSource> WalkRight,
    IReadOnlyList<ImageSource> WalkLeft,
    IReadOnlyList<ImageSource> IdleRight,
    IReadOnlyList<ImageSource> IdleLeft,
    IReadOnlyList<ImageSource> SleepRight,
    IReadOnlyList<ImageSource> SleepLeft,
    double VerticalOffset = 0); // Pixels transparentes do topo para ajustar alinhamento
