using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace mdviewx.Presentation;

/// <summary>A panel that shows a horizontal-resize cursor while hovered.</summary>
public sealed partial class ResizeGrip : Grid
{
    public ResizeGrip()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    }
}
