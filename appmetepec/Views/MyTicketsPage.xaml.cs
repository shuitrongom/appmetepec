using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class MyTicketsPage : ContentPage
{
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;

    public MyTicketsPage(MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _api = api;
        _navigationState = navigationState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (TicketsView.ItemsSource is null)
        {
            await LoadTicketsAsync();
        }
    }

    private async Task LoadTicketsAsync()
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            TicketsView.ItemsSource = await _api.GetMyTicketsAsync();
        }
        catch (Exception ex)
        {
            TicketsView.ItemsSource = Array.Empty<BackendTicketDto>();
            await DisplayAlert("No se pudieron cargar tus reportes", ex.Message, "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadTicketsAsync();
        TicketsRefreshView.IsRefreshing = false;
    }

    private async void OnTicketSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not BackendTicketDto ticket)
        {
            return;
        }

        ((CollectionView)sender).SelectedItem = null;
        _navigationState.SelectedTicket = ticket;
        await Shell.Current.GoToAsync(nameof(TicketDetailPage));
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
