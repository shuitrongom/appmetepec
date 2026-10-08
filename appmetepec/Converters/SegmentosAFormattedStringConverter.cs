using System.Globalization;
using appmetepec.Models;

namespace appmetepec.Converters;

// Arma el texto de una respuesta de ticket: los segmentos con Url se pintan como enlace
// (subrayado, color de marca). El toque NO va en cada Span (en Android MAUI recalcula las
// posiciones de los Span con gestos en cada layout y bloqueaba la UI); lo maneja el Label
// completo en TicketDetailPage.OnRespuestaTapped.
public sealed class SegmentosAFormattedStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var formatted = new FormattedString();
        if (value is not IEnumerable<SegmentoTexto> segmentos)
        {
            return formatted;
        }

        var colorLink = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#6CB6E8")
            : Color.FromArgb("#1F6FB2");

        foreach (var segmento in segmentos)
        {
            var span = new Span { Text = segmento.Texto };
            if (segmento.Url is not null && Uri.TryCreate(segmento.Url, UriKind.Absolute, out _))
            {
                span.TextColor = colorLink;
                span.TextDecorations = TextDecorations.Underline;
            }
            formatted.Spans.Add(span);
        }

        return formatted;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
