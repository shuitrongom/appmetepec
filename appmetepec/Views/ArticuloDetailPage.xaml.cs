using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class ArticuloDetailPage : ContentPage
{
    private readonly NavigationState _navigationState;
    private BackendArticuloConocimientoDto? _articulo;

    public ArticuloDetailPage(NavigationState navigationState)
    {
        InitializeComponent();
        _navigationState = navigationState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _articulo = _navigationState.SelectedArticulo;
        if (_articulo is null)
        {
            Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = _articulo.Titulo;
        ContentLabel.Text = _articulo.Contenido;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
