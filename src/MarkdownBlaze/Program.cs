using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Photino.Blazor;
using MarkdownBlaze;
using MarkdownBlaze.Services;

internal static class Program
{
    // WebView2 on Windows requires an STA thread; top-level statements run as MTA,
    // which leaves the WebView uninitialized (blank/black window).
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        builder.Services.AddLogging();
        builder.Services.AddFluentUIComponents();

        builder.Services.AddSingleton<PreferencesStore>();
        builder.Services.AddSingleton<HistoryStore>();
        builder.Services.AddSingleton<MarkdownService>();
        builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<WindowHost>();

        builder.RootComponents.Add<App>("app");

        var app = builder.Build();

        app.MainWindow
            .SetTitle("MarkdownBlaze")
            .SetSize(1280, 860);

        // Set the window icon. On Windows, push the .ico onto the HWND via WM_SETICON once the native
        // window exists (Photino's SetIconFile only sets it there); other platforms use SetIconFile.
        if (OperatingSystem.IsWindows())
        {
            var ico = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            if (File.Exists(ico))
                app.MainWindow.RegisterWindowCreatedHandler((_, _) => NativeIcon.Apply(app.MainWindow.WindowHandle, ico));
        }
        else
        {
            var png = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
            if (File.Exists(png))
                try { app.MainWindow.SetIconFile(png); } catch { /* best-effort */ }
        }

        // Expose the window so components can use native dialogs (e.g. the "open file" button).
        app.Services.GetRequiredService<WindowHost>().Window = app.MainWindow;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine("Unhandled exception: " + e.ExceptionObject);

        app.Run();
    }
}
