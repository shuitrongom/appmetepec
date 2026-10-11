using Foundation;
using UIKit;
using UserNotifications;

namespace appmetepec
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        // --- FIX notificaciones locales en iOS ---
        //
        // PROBLEMA (causa raiz): en iOS solo puede haber UN delegate del centro de notificaciones
        // (UNUserNotificationCenter.Delegate). Plugin.Firebase (CloudMessaging) se registra como
        // ese delegate al inicializarse en MauiProgram (WillFinishLaunching), lo que PISA el
        // delegate que Plugin.LocalNotification necesita para ENTREGAR las notificaciones locales.
        // Resultado: en iOS los recordatorios locales nunca se mostraban (en Android si, porque
        // Android no usa este mecanismo de delegate unico). Es un conflicto conocido Firebase vs
        // notificaciones locales en iOS.
        //
        // SOLUCION (de raiz): tras terminar el arranque (FinishedLaunching, que corre DESPUES de
        // WillFinishLaunching donde arranca Firebase), instalamos NUESTRO propio delegate del
        // centro de notificaciones. Ese delegate:
        //   - Presenta las notificaciones en PRIMER PLANO (banner + sonido + badge): por defecto
        //     iOS NO muestra nada si la app esta en foreground; hay que pedirlo explicitamente en
        //     WillPresentNotification. Asi el recordatorio se ve este la app abierta o no.
        //   - Reenvia los eventos de push REMOTO (Firebase) a su delegate original, para NO romper
        //     el push del servidor: guardamos el delegate que Firebase dejo y le delegamos lo que
        //     no sea responsabilidad del aviso local.
        //
        // Esto no toca Android ni el flujo de Firebase remoto; solo garantiza que iOS entregue las
        // notificaciones locales ademas de las remotas.
        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            var resultado = base.FinishedLaunching(application, launchOptions);

            if (OperatingSystem.IsIOSVersionAtLeast(10))
            {
                var centro = UNUserNotificationCenter.Current;
                // Guardamos el delegate que haya quedado (el de Firebase, si lo instalo) para
                // reenviarle los eventos de push remoto y no romper esa funcionalidad.
                var delegadoPrevio = centro.Delegate;
                centro.Delegate = new DelegadoNotificaciones(delegadoPrevio);
            }

            return resultado;
        }
    }

    // Delegate del centro de notificaciones de iOS que convive con Firebase:
    //  - Muestra las notificaciones LOCALES (y cualquier otra) en primer plano.
    //  - Reenvia al delegate ORIGINAL (Firebase) los callbacks de push remoto para preservarlo.
    internal sealed class DelegadoNotificaciones : UNUserNotificationCenterDelegate
    {
        private readonly IUNUserNotificationCenterDelegate? _previo;

        public DelegadoNotificaciones(IUNUserNotificationCenterDelegate? previo)
        {
            _previo = previo;
        }

        // Se llama cuando llega una notificacion con la app en PRIMER PLANO. Pedimos a iOS que la
        // muestre igual (banner + sonido + badge); sin esto, en foreground no se ve nada.
        public override void WillPresentNotification(
            UNUserNotificationCenter center,
            UNNotification notification,
            Action<UNNotificationPresentationOptions> completionHandler)
        {
            completionHandler(
                UNNotificationPresentationOptions.Banner |
                UNNotificationPresentationOptions.Sound |
                UNNotificationPresentationOptions.Badge);
        }

        // Se llama cuando el usuario TOCA una notificacion. Si el delegate previo (Firebase)
        // existe y sabe manejar este callback, se lo reenviamos (para no romper el push remoto);
        // si no, completamos sin romper nada. La comprobacion RespondsToSelector se hace sobre el
        // NSObject subyacente (la interfaz IUNUserNotificationCenterDelegate no la expone).
        public override void DidReceiveNotificationResponse(
            UNUserNotificationCenter center,
            UNNotificationResponse response,
            Action completionHandler)
        {
            var selector = new ObjCRuntime.Selector("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:");

            if (_previo is NSObject previoNs && previoNs.RespondsToSelector(selector))
            {
                _previo.DidReceiveNotificationResponse(center, response, completionHandler);
            }
            else
            {
                completionHandler();
            }
        }
    }
}
