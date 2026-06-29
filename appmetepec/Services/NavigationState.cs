using appmetepec.Models;

namespace appmetepec.Services;

public sealed class NavigationState
{
    public ScreenReport? SelectedReport { get; set; }
    public NewsLetter? SelectedNews { get; set; }
}
