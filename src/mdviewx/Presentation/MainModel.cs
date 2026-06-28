namespace mdviewx.Presentation;

public partial record MainModel
{
    private readonly INavigator _navigator;

    public MainModel(INavigator navigator)
    {
        _navigator = navigator;
    }

    public async Task GoToSettings()
    {
        await _navigator.NavigateViewModelAsync<SettingsModel>(this);
    }
}
