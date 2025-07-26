using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowTranslator.ComponentModel;

[ObservableObject]
public partial class BusyScope
{

    [ObservableProperty]
    private bool isBusy = false;

    public IDisposable EnterBusy()
    {
        this.IsBusy = true;
        return new DisposeAction(() => this.IsBusy = false);
    }

}
