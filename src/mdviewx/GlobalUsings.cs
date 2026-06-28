global using System.Collections.Immutable;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Localization;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using mdviewx.Models;
global using mdviewx.Presentation;
global using mdviewx.Services;
#if MAUI_EMBEDDING
global using mdviewx.MauiControls;
#endif
global using ApplicationExecutionState = Windows.ApplicationModel.Activation.ApplicationExecutionState;
global using Color = Windows.UI.Color;
[assembly: Uno.Extensions.Reactive.Config.BindableGenerationTool(3)]
