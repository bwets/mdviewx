namespace mdviewx.Presentation;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        this.DataContext<SettingsViewModel>((page, vm) => page
            .Background(ThemeResource.Get<Brush>("ApplicationPageBackgroundThemeBrush"))
            .Content(new Grid()
                .SafeArea(SafeArea.InsetMask.VisibleBounds)
                .RowDefinitions("Auto,*")
                .Children(
                    new NavigationBar()
                        .Content("Settings")
                        .MainCommand(
                            new AppBarButton()
                                .Icon(new SymbolIcon(Symbol.Back))
                                .AutomationProperties(automationId: "BackButton")
                                .Command(() => vm.GoBack)),
                    new StackPanel()
                        .Grid(row: 1)
                        .Margin(24)
                        .Spacing(16)
                        .Children(
                            new TextBlock()
                                .Text("Settings")
                                .Style(ThemeResource.Get<Style>("TitleTextBlockStyle")),
                            new TextBlock()
                                .Text("Nothing to configure yet.")
                                .Foreground(ThemeResource.Get<Brush>("TextFillColorSecondaryBrush"))))));
    }
}
