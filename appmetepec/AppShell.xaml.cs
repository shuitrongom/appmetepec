using appmetepec.Views;

namespace appmetepec
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(ReportPage), typeof(ReportPage));
            Routing.RegisterRoute(nameof(ReportSuccessPage), typeof(ReportSuccessPage));
            Routing.RegisterRoute(nameof(NewsDetailPage), typeof(NewsDetailPage));
            Routing.RegisterRoute(nameof(RecoleccionPage), typeof(RecoleccionPage));
            Routing.RegisterRoute(nameof(AlertaNaranjaPage), typeof(AlertaNaranjaPage));
        }
    }
}
