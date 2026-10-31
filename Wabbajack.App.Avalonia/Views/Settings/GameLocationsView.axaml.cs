using Avalonia.ReactiveUI;
using ReactiveUI;
using Wabbajack.App.Avalonia.ViewModels.Settings;

namespace Wabbajack.App.Avalonia.Views.Settings;

public partial class GameLocationsView : ReactiveUserControl<GameLocationsVM>
{
    public GameLocationsView()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });
    }
}
