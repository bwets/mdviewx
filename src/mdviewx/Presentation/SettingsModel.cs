namespace mdviewx.Presentation;

public partial record SettingsModel
{
    private readonly INavigator _navigator;

    public SettingsModel(INavigator navigator)
    {
        _navigator = navigator;
    }

    public async Task GoBack()
    {
        await _navigator.NavigateBackAsync(this);
    }
}
