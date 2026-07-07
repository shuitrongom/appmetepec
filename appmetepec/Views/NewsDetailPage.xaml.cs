using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class NewsDetailPage : ContentPage
{
    private readonly NavigationState _navigationState;
    private NewsLetter? _news;

    public NewsDetailPage(NavigationState navigationState)
    {
        InitializeComponent();
        _navigationState = navigationState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _news = _navigationState.SelectedNews;
        if (_news is null)
        {
            Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = _news.title;
        SubtitleLabel.Text = _news.subtitle;
        ContentLabel.Text = _news.content ?? _news.shortContent;
        OpenUrlButton.IsVisible = !string.IsNullOrWhiteSpace(_news.url);

        if (!string.IsNullOrWhiteSpace(_news.image))
        {
            NewsImage.Source = ImageSource.FromUri(new Uri(_news.image));
            NewsImage.IsVisible = true;
        }
    }

    private async void OnOpenUrlClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_news?.url))
        {
            await Launcher.Default.OpenAsync(_news.url);
        }
    }
}
