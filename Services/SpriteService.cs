using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PokeBar.Models;

namespace PokeBar.Services;

public class SpriteService
{
    public string SpriteRoot { get; private set; } = string.Empty;
    private readonly GameState _state;
    public event EventHandler<string>? SpriteRootChanged;

    public SpriteService(GameState state)
    {
        _state = state;
        UpdateSpriteRoot();
    }

    public void ApplySpriteRoot(string? newRoot)
    {
        _state.SpriteRootPath = string.IsNullOrWhiteSpace(newRoot) ? null : newRoot;
        UpdateSpriteRoot();
    }

    public void UpdateSpriteRoot()
    {
        var previous = SpriteRoot;
        // Prioridade:
        // 1. GameState.SpriteRootPath (configuração do usuário)
        // 2. Variável de ambiente POKEBAR_SPRITE_ROOT
        // 3. Pasta relativa ./sprites/ (se existir)
        // 4. Fallback hardcoded (para desenvolvimento)
        
        var relativePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites");
        var hardcodedPath = "C:\\Users\\Arthur\\Documents\\Projetos\\SpriteCollab\\sprite";
        
        SpriteRoot = _state.SpriteRootPath 
                     ?? Environment.GetEnvironmentVariable("POKEBAR_SPRITE_ROOT")
                     ?? (Directory.Exists(relativePath) ? relativePath : hardcodedPath);

        if (!string.Equals(previous, SpriteRoot, StringComparison.OrdinalIgnoreCase))
        {
            SpriteRootChanged?.Invoke(this, SpriteRoot);
        }
    }

    public string GetPortraitPath(int dexNumber)
    {
        // Para portraits, usar subpasta "portrait" ao invés de "sprite"
        var portraitRoot = SpriteRoot.Replace("\\sprite", "\\portrait");
        return Path.Combine(portraitRoot, dexNumber.ToString("D4"), "Normal.png");
    }

    public SpriteAnimationSet LoadAnimations(int dexNumber)
    {
        var (walkRight, walkLeft) = LoadWalkFrames(dexNumber);
        var folder = Path.Combine(SpriteRoot, dexNumber.ToString("D4"));

        var (idleRightRaw, idleLeftRaw) = LoadDirectionalFromFile(Path.Combine(folder, "Idle-Anim.png"), 3, 7);
        var idleRight = idleRightRaw.Count > 0 ? idleRightRaw : walkRight;
        var idleLeft = idleLeftRaw.Count > 0 ? idleLeftRaw : walkLeft;

        var (sleepRightRaw, sleepLeftRaw) = LoadDirectionalFromFile(Path.Combine(folder, "Sleep-Anim.png"));
        var sleepRight = sleepRightRaw.Count > 0 ? sleepRightRaw : idleRight;
        var sleepLeft = sleepLeftRaw.Count > 0 ? sleepLeftRaw : idleLeft;

        // Calcular offset vertical baseado no primeiro frame de walk
        double verticalOffset = CalculateVerticalOffset(walkRight.Count > 0 ? walkRight[0] : null);

        return new SpriteAnimationSet(walkRight, walkLeft, idleRight, idleLeft, sleepRight, sleepLeft, verticalOffset);
    }

    public (IReadOnlyList<ImageSource> right, IReadOnlyList<ImageSource> left) LoadWalkFrames(int dexNumber)
    {
        var dex = dexNumber.ToString("D4");
        var folder = Path.Combine(SpriteRoot, dex);
        var walk = Path.Combine(folder, "Walk-Anim.png");
        if (File.Exists(walk))
        {
            // PMD: row 3 = right facing, row 7 = left facing
            // Mas vamos inverter se necessário
            var (row3Frames, row7Frames) = FromPMDSheetAuto(walk, rightRow1Based: 3, leftRow1Based: 7);
            
            // Se ambos existem, usa direto
            if (row3Frames.Count > 0 && row7Frames.Count > 0)
            {
                return (row3Frames, row7Frames);
            }
            
            // Se só tem uma direção, espelha para a outra
            if (row3Frames.Count > 0)
            {
                return (row3Frames, MirrorFrames(row3Frames));
            }
            
            if (row7Frames.Count > 0)
            {
                return (MirrorFrames(row7Frames), row7Frames);
            }
        }

        // fallback: look for frames in assets/sprites/name/*.png like left/right_*.png
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.png").OrderBy(x => x).ToArray() : Array.Empty<string>();
        if (files.Length > 0)
        {
            var imgs = files.Select(f => (ImageSource)new BitmapImage(new Uri(f))).ToList();
            var mirrored = MirrorFrames(imgs);
            return (imgs, mirrored);
        }

        // Solid color fallback
        var fallback = CreateSolidFallback();
        return (fallback, fallback);
    }

    private (IReadOnlyList<ImageSource> right, IReadOnlyList<ImageSource> left) LoadDirectionalFromFile(string path, int? rightRow1Based = null, int? leftRow1Based = null)
    {
        if (!File.Exists(path))
        {
            return (Array.Empty<ImageSource>(), Array.Empty<ImageSource>());
        }
        if (rightRow1Based.HasValue && leftRow1Based.HasValue)
        {
            var byRows = FromPMDSheetAuto(path, rightRow1Based.Value, leftRow1Based.Value);
            if (byRows.right.Count > 0 || byRows.left.Count > 0)
            {
                return byRows;
            }
        }

        var fallback = SliceFullHeightByAlpha(path);
        if (fallback.Count == 0)
        {
            fallback = SliceWholeSheet(path);
        }
        if (fallback.Count == 0)
        {
            return (Array.Empty<ImageSource>(), Array.Empty<ImageSource>());
        }
        return (fallback, MirrorFrames(fallback));
    }

    private static (IReadOnlyList<ImageSource> right, IReadOnlyList<ImageSource> left) FromPMDSheetAuto(string path, int rightRow1Based, int leftRow1Based)
    {
        var bmpRaw = new BitmapImage(new Uri(path));
        // Converter para Pbgra32 para analisarmos alpha facilmente
        var bmp = new FormatConvertedBitmap(bmpRaw, PixelFormats.Pbgra32, null, 0);
        int rows = 8; // PMD SpriteCollab typical
        // Altura de cada frame é a altura total dividida pelas linhas
        int frameHeight = bmp.PixelHeight / rows;
        int rightRow = rightRow1Based - 1;
        int leftRow = leftRow1Based - 1;

        var rightFrames = SliceRowByAlpha(bmp, rightRow, frameHeight);
        var leftFrames = SliceRowByAlpha(bmp, leftRow, frameHeight);

        if (rightFrames.Count == 0 && leftFrames.Count == 0)
        {
            var fb = CreateSolidFallback();
            return (fb, fb);
        }

        // Normaliza contagem para evitar descompasso na animação
        int count = Math.Max(rightFrames.Count, leftFrames.Count);
        if (rightFrames.Count == 0) rightFrames = leftFrames.ToList();
        if (leftFrames.Count == 0) leftFrames = rightFrames.ToList();
        return (rightFrames, leftFrames);
    }

    private static List<ImageSource> SliceRowByAlpha(BitmapSource src, int row, int frameHeight)
    {
        int y = row * frameHeight;
        var rowRect = new System.Windows.Int32Rect(0, y, src.PixelWidth, frameHeight);
        int stride = src.PixelWidth * 4;
        var buffer = new byte[stride * frameHeight];
        src.CopyPixels(rowRect, buffer, stride, 0);

        bool ColumnHasAlpha(int x)
        {
            for (int yy = 0; yy < frameHeight; yy++)
            {
                int idx = yy * stride + x * 4 + 3; // A
                if (buffer[idx] > 0) return true;
            }
            return false;
        }

        var segments = new List<(int start, int end)>();
        bool inSeg = false;
        int segStart = 0;
        int width = src.PixelWidth;
        for (int x = 0; x < width; x++)
        {
            bool has = ColumnHasAlpha(x);
            if (has && !inSeg)
            {
                inSeg = true;
                segStart = x;
            }
            else if (!has && inSeg)
            {
                inSeg = false;
                segments.Add((segStart, x - 1));
            }
        }
        if (inSeg) segments.Add((segStart, width - 1));

        // Filtra segmentos muito pequenos e limita a um número razoável de frames
        segments = segments.Where(s => (s.end - s.start + 1) >= 4).Take(24).ToList();

        var list = new List<ImageSource>();
        foreach (var (start, end) in segments)
        {
            var rc = new System.Windows.Int32Rect(start, y, end - start + 1, frameHeight);
            list.Add(CropAndCenter(src, rc, frameHeight, frameHeight));
        }
        return list;
    }

    private static IReadOnlyList<ImageSource> SliceFullHeightByAlpha(string path)
    {
        try
        {
            var bmpRaw = new BitmapImage(new Uri(path));
            var bmp = new FormatConvertedBitmap(bmpRaw, PixelFormats.Pbgra32, null, 0);
            return SliceRowByAlpha(bmp, 0, bmp.PixelHeight);
        }
        catch
        {
            return Array.Empty<ImageSource>();
        }
    }

    private static IReadOnlyList<ImageSource> SliceWholeSheet(string path)
    {
        try
        {
            var bmpRaw = new BitmapImage(new Uri(path));
            var bmp = new FormatConvertedBitmap(bmpRaw, PixelFormats.Pbgra32, null, 0);
            return SliceSquareGrid(bmp);
        }
        catch
        {
            return Array.Empty<ImageSource>();
        }
    }

    private static IReadOnlyList<ImageSource> SliceSquareGrid(BitmapSource bmp)
    {
        int gcd = GreatestCommonDivisor(bmp.PixelWidth, bmp.PixelHeight);
        int size = gcd >= 16 ? gcd : bmp.PixelHeight;
        if (size <= 0)
        {
            size = Math.Min(Math.Max(1, bmp.PixelWidth), Math.Max(1, bmp.PixelHeight));
        }
        int rows = Math.Max(1, bmp.PixelHeight / size);
        int cols = Math.Max(1, (int)Math.Ceiling(bmp.PixelWidth / (double)size));
        var list = new List<ImageSource>(cols * rows);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int width = Math.Min(size, bmp.PixelWidth - c * size);
                int height = Math.Min(size, bmp.PixelHeight - r * size);
                if (width <= 0 || height <= 0)
                    continue;
                var rect = new System.Windows.Int32Rect(c * size, r * size, width, height);
                list.Add(CropAndCenter(bmp, rect, size, size));
            }
        }
        return list;
    }

    private static IReadOnlyList<ImageSource> MirrorFrames(IReadOnlyList<ImageSource> frames)
    {
        var mirrored = new List<ImageSource>(frames.Count);
        foreach (var frame in frames)
        {
            if (frame is not BitmapSource bmp)
            {
                mirrored.Add(frame);
                continue;
            }
            var transformed = new TransformedBitmap(bmp, new ScaleTransform(-1, 1, bmp.PixelWidth / 2.0, bmp.PixelHeight / 2.0));
            transformed.Freeze();
            mirrored.Add(transformed);
        }
        return mirrored;
    }

    private static ImageSource CropAndCenter(BitmapSource src, System.Windows.Int32Rect area, int targetWidth, int targetHeight)
    {
        var cb = new CroppedBitmap(src, area);
        cb.Freeze();
        if (area.Width == targetWidth && area.Height == targetHeight)
            return cb;

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            double offsetX = (targetWidth - area.Width) / 2.0;
            double offsetY = (targetHeight - area.Height) / 2.0;
            dc.DrawImage(cb, new System.Windows.Rect(offsetX, offsetY, area.Width, area.Height));
        }
        var rtb = new RenderTargetBitmap(targetWidth, targetHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    private static IReadOnlyList<ImageSource> CreateSolidFallback()
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF2, 0x57, 0x64)), null, new System.Windows.Rect(0, 0, 48, 48));
        }
        var rtb = new RenderTargetBitmap(48, 48, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return new[] { (ImageSource)rtb };
    }

    private static double CalculateVerticalOffset(ImageSource? sprite)
    {
        if (sprite is not BitmapSource bmp)
            return 0;

        try
        {
            var converted = new FormatConvertedBitmap(bmp, PixelFormats.Pbgra32, null, 0);
            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            var pixels = new byte[stride * height];
            converted.CopyPixels(pixels, stride, 0);

            // Encontrar primeira linha visível (topo do sprite real)
            int firstVisibleY = 0;
            for (int y = 0; y < height; y++)
            {
                bool hasPixel = false;
                for (int x = 0; x < width; x++)
                {
                    int alphaIndex = (y * stride) + (x * 4) + 3;
                    if (pixels[alphaIndex] > 0)
                    {
                        hasPixel = true;
                        break;
                    }
                }
                if (hasPixel)
                {
                    firstVisibleY = y;
                    break;
                }
            }

            // Encontrar última linha visível (base do sprite real)
            int lastVisibleY = height - 1;
            for (int y = height - 1; y >= 0; y--)
            {
                bool hasPixel = false;
                for (int x = 0; x < width; x++)
                {
                    int alphaIndex = (y * stride) + (x * 4) + 3;
                    if (pixels[alphaIndex] > 0)
                    {
                        hasPixel = true;
                        break;
                    }
                }
                if (hasPixel)
                {
                    lastVisibleY = y;
                    break;
                }
            }

            // Calcular o padding superior (espaço transparente acima do sprite)
            int topPadding = firstVisibleY;
            
            // Retornar o topPadding positivo para que quando aplicado com sinal negativo
            // no SpriteWindow (PlayerTranslate.Y = -offset), empurre o sprite para baixo
            return topPadding;
        }
        catch
        {
            // Se falhar, retorna 0
        }

        return 0;
    }
}
