using appmetepec.Views;

namespace appmetepec
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(ReportPage), typeof(ReportPage));
            Routing.RegisterRoute(nameof(ReportSuccessPage), typeof(ReportSuccessPage));
            Routing.RegisterRoute(nameof(NewsDetailPage), typeof(NewsDetailPage));
            Routing.RegisterRoute(nameof(RecoleccionPage), typeof(RecoleccionPage));
            Routing.RegisterRoute(nameof(AlertaNaranjaPage), typeof(AlertaNaranjaPage));
            Routing.RegisterRoute(nameof(MyTicketsPage), typeof(MyTicketsPage));
            Routing.RegisterRoute(nameof(TicketDetailPage), typeof(TicketDetailPage));
            Routing.RegisterRoute(nameof(EncuestaPage), typeof(EncuestaPage));
        }
    }
}
