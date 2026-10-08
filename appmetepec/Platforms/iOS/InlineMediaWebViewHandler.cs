using Microsoft.Maui.Handlers;
using WebKit;

namespace appmetepec.Platforms.iOS;

// Handler propio del <WebView> en iOS. El reproductor de YouTube de las noticias
// (NewsDetailPage) carga un iframe embebido en un WKWebView. Por defecto WebKit NO reproduce
// medios inline y exige un gesto del usuario por cada medio, asi que el iframe de YouTube se ve
// pero no arranca al dar play. Esas dos opciones (AllowsInlineMediaPlayback y
// MediaTypesRequiringUserActionForPlayback) viven en WKWebViewConfiguration y WebKit solo las
// respeta si se fijan ANTES de instanciar el WKWebView; un mapping del handler correria tarde
// (el WebView ya existe), por eso se intercepta la creacion del PlatformView.
//
// Es el unico <WebView> de la app (el mapa usa HybridWebView, otro handler), por lo que
// reemplazar el handler global no afecta a nada mas.
public sealed class InlineMediaWebViewHandler : WebViewHandler
{
    protected override WKWebView CreatePlatformView()
    {
        var config = new WKWebViewConfiguration
        {
            AllowsInlineMediaPlayback = true,
            MediaTypesRequiringUserActionForPlayback = WKAudiovisualMediaTypes.None,
        };

        return new WKWebView(CoreGraphics.CGRect.Empty, config);
    }
}
