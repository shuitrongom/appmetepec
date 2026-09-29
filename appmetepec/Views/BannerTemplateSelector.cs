using appmetepec.Models;

namespace appmetepec.Views;

// El BannerCarousel de HomePage mezcla dos tipos de slide: los banners promocionales fijos
// (BannerItem, definido dentro de HomePage) y las noticias destacadas+vigentes (NewsLetter,
// agregadas al final por HomePage.ShowNews). No hace falta distinguir el tipo exacto del banner
// fijo: cualquier item que no sea NewsLetter se trata como banner promocional.
public sealed class BannerTemplateSelector : DataTemplateSelector
{
    public DataTemplate? PromoTemplate { get; set; }
    public DataTemplate? NewsTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container) =>
        item is NewsLetter ? NewsTemplate : PromoTemplate;
}
