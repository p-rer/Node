using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Node.Editor.ViewModel;

namespace Node.Editor.View;

public class ConnectionAdorner : Adorner
{
    private const double RejectionPopupMaxTextWidth = 320;
    private const double RejectionPopupPadding = 6;

    /// <summary>マウスカーソルと重ならないようにするための、ポップアップのカーソルからの距離。</summary>
    private const double RejectionPopupCursorOffset = 18;

    public ConnectionAdorner(UIElement adornedElement)
        : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (DataContext is not GraphViewModel vm)
            return;

        if (vm is { DraggingFromPort: not null, TemporaryEndPoint: not null })
        {
            var start = vm.TransformToScreen(vm.DraggingFromPort.Position);
            var end = vm.TemporaryEndPoint.Value;
            var rejectionMessage = vm.ConnectionRejectionMessage;
            var isRejected = rejectionMessage is not null;
            dc.DrawLine(new Pen(isRejected ? Brushes.Red : Brushes.Orange, 2), start, end);

            if (rejectionMessage is not null)
                DrawRejectionPopup(dc, rejectionMessage, end);
        }
    }

    private void DrawRejectionPopup(DrawingContext dc, string message, Point cursor)
    {
        var typeface = new Typeface(
            SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle,
            SystemFonts.MessageFontWeight, FontStretches.Normal);
        var text = new FormattedText(
            message,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            typeface,
            SystemFonts.MessageFontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = RejectionPopupMaxTextWidth
        };
        text.MaxTextWidth = Math.Ceiling(text.Width) + 1;

        var size = new Size(
            text.MaxTextWidth + RejectionPopupPadding * 2,
            text.Height + RejectionPopupPadding * 2);

        var bounds = AdornedElement.RenderSize;
        var x = cursor.X + RejectionPopupCursorOffset;
        var y = cursor.Y + RejectionPopupCursorOffset;
        if (x + size.Width > bounds.Width)
            x = cursor.X - RejectionPopupCursorOffset - size.Width;
        if (y + size.Height > bounds.Height)
            y = cursor.Y - RejectionPopupCursorOffset - size.Height;
        x = Math.Max(0, x);
        y = Math.Max(0, y);

        dc.DrawRoundedRectangle(Brushes.Red, new Pen(Brushes.Transparent, 0), new Rect(new Point(x, y), size), 0, 0);
        dc.DrawText(text, new Point(x + RejectionPopupPadding, y + RejectionPopupPadding));
    }
}