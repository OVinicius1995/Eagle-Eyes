using System.Windows.Media;
using System.Windows.Shapes;

namespace EagleEyes.App;

internal static class PenPalette
{
    internal static readonly Color Red = Colors.Red;
    internal static readonly (string Name, Color Color)[] ColorsByName =
        [("Vermelho", Red), ("Amarelo", Colors.Gold), ("Azul", Colors.DeepSkyBlue)];

    internal static string PencilHint(bool active) => active
        ? "Lápis ligado. Clique no lápis novamente para parar de desenhar."
        : "Clique no lápis para desenhar. Clique nele novamente para sair.";

    internal static string ColorHint(string name) =>
        $"Clique para desenhar em {name.ToLowerInvariant()}. Escolher uma cor também liga o lápis.";

    internal static Ellipse Swatch(Color color, bool selected) => new()
    {
        Width = 13, Height = 13, Fill = new SolidColorBrush(color),
        Stroke = selected ? Brushes.White : null, StrokeThickness = 2
    };
}