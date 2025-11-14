using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace PokeBar.Utils;

public class PortraitPathConverter : IValueConverter
{
    public static string? SpriteRootPath { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int dexNumber)
            return string.Empty;

        // Prioridade:
        // 1. SpriteRootPath configurado
        // 2. Variável de ambiente
        // 3. Pasta relativa ./sprites/portrait/
        
        var spriteRoot = SpriteRootPath 
                         ?? Environment.GetEnvironmentVariable("POKEBAR_SPRITE_ROOT")
                         ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sprites");

        // Converter de sprite/ para portrait/
        var portraitRoot = spriteRoot.Replace("\\sprite", "\\portrait")
                                     .Replace("/sprite", "/portrait");
        
        // Se não tem /sprite no path, adicionar /portrait
        if (!portraitRoot.Contains("portrait"))
        {
            portraitRoot = Path.Combine(portraitRoot, "portrait");
        }

        // Verificar se o diretório portrait existe
        if (!Directory.Exists(portraitRoot))
        {
            // Tentar caminho relativo como fallback
            var relativePortrait = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portrait");
            if (Directory.Exists(relativePortrait))
            {
                portraitRoot = relativePortrait;
            }
            else
            {
                // Sem portraits disponíveis, retornar caminho vazio
                return string.Empty;
            }
        }

        var path = Path.Combine(portraitRoot, dexNumber.ToString("D4"), "Normal.png");
        
        // Verificar se o arquivo existe antes de retornar
        return File.Exists(path) ? path : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
