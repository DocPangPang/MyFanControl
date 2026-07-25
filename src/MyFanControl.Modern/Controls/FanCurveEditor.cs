using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MyFanControl.Modern.Models;
using Brush = System.Windows.Media.Brush;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace MyFanControl.Modern.Controls;

public sealed class FanCurveEditor : FrameworkElement
{
    private const double MinTemperature = 30;
    private const double MaxTemperature = 100;
    private const double MinDuty = 0;
    private const double MaxDuty = 100;
    private readonly Pen _gridPen = new(new SolidColorBrush(Color.FromArgb(70, 98, 117, 145)), 1);
    private int _dragIndex = -1;

    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points),
        typeof(IList<FanCurvePoint>),
        typeof(FanCurveEditor),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));

    public static readonly DependencyProperty CurveBrushProperty = DependencyProperty.Register(
        nameof(CurveBrush),
        typeof(Brush),
        typeof(FanCurveEditor),
        new FrameworkPropertyMetadata(Brushes.CornflowerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public IList<FanCurvePoint>? Points
    {
        get => (IList<FanCurvePoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public Brush CurveBrush
    {
        get => (Brush)GetValue(CurveBrushProperty);
        set => SetValue(CurveBrushProperty, value);
    }

    public FanCurveEditor()
    {
        Cursor = Cursors.Cross;
        Focusable = true;
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        Rect plot = GetPlotRect();
        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(15, 23, 35)),
            new Pen(new SolidColorBrush(Color.FromRgb(38, 51, 72)), 1),
            new Rect(0, 0, ActualWidth, ActualHeight),
            12,
            12);

        DrawGrid(drawingContext, plot);
        if (Points is null || Points.Count == 0)
            return;

        Point[] screenPoints = Points.Select(ToScreen).ToArray();
        var fillGeometry = new StreamGeometry();
        using (StreamGeometryContext context = fillGeometry.Open())
        {
            context.BeginFigure(new Point(screenPoints[0].X, plot.Bottom), true, true);
            context.LineTo(screenPoints[0], true, false);
            foreach (Point point in screenPoints.Skip(1))
                context.LineTo(point, true, false);
            context.LineTo(new Point(screenPoints[^1].X, plot.Bottom), true, false);
        }

        Brush fill = CurveBrush.Clone();
        fill.Opacity = 0.14;
        drawingContext.DrawGeometry(fill, null, fillGeometry);

        var lineGeometry = new StreamGeometry();
        using (StreamGeometryContext context = lineGeometry.Open())
        {
            context.BeginFigure(screenPoints[0], false, false);
            foreach (Point point in screenPoints.Skip(1))
                context.LineTo(point, true, false);
        }
        drawingContext.DrawGeometry(null, new Pen(CurveBrush, 3), lineGeometry);

        for (int index = 0; index < screenPoints.Length; index++)
        {
            Point point = screenPoints[index];
            drawingContext.DrawEllipse(
                index == _dragIndex ? Brushes.White : CurveBrush,
                new Pen(new SolidColorBrush(Color.FromRgb(11, 16, 24)), 2),
                point,
                index == _dragIndex ? 7 : 6,
                index == _dragIndex ? 7 : 6);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (Points is null)
            return;

        Point mouse = e.GetPosition(this);
        double bestDistance = 18;
        for (int index = 0; index < Points.Count; index++)
        {
            double distance = (ToScreen(Points[index]) - mouse).Length;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                _dragIndex = index;
            }
        }

        if (_dragIndex >= 0)
        {
            CaptureMouse();
            UpdateDraggedPoint(mouse);
            InvalidateVisual();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragIndex < 0 || e.LeftButton != MouseButtonState.Pressed)
            return;

        UpdateDraggedPoint(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragIndex = -1;
        ReleaseMouseCapture();
        InvalidateVisual();
    }

    private void UpdateDraggedPoint(Point mouse)
    {
        if (Points is null || _dragIndex < 0 || _dragIndex >= Points.Count)
            return;

        Rect plot = GetPlotRect();
        double temperature = MinTemperature +
            Math.Clamp((mouse.X - plot.Left) / plot.Width, 0, 1) * (MaxTemperature - MinTemperature);
        double duty = MaxDuty -
            Math.Clamp((mouse.Y - plot.Top) / plot.Height, 0, 1) * (MaxDuty - MinDuty);

        double lowerLimit = _dragIndex == 0 ? MinTemperature : Points[_dragIndex - 1].Temperature + 2;
        double upperLimit = _dragIndex == Points.Count - 1
            ? MaxTemperature
            : Points[_dragIndex + 1].Temperature - 2;

        Points[_dragIndex].Temperature = Math.Clamp(temperature, lowerLimit, upperLimit);
        Points[_dragIndex].Duty = Math.Clamp(duty, MinDuty, MaxDuty);
        InvalidateVisual();
    }

    private void DrawGrid(DrawingContext drawingContext, Rect plot)
    {
        var typeface = new Typeface("Segoe UI");
        for (int duty = 0; duty <= 100; duty += 25)
        {
            double y = plot.Bottom - duty / 100d * plot.Height;
            drawingContext.DrawLine(_gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawLabel(drawingContext, $"{duty}%", new Point(7, y - 8), typeface);
        }

        for (int temperature = 30; temperature <= 100; temperature += 10)
        {
            double x = plot.Left + (temperature - MinTemperature) /
                (MaxTemperature - MinTemperature) * plot.Width;
            drawingContext.DrawLine(_gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            DrawLabel(drawingContext, $"{temperature}°", new Point(x - 11, plot.Bottom + 7), typeface);
        }
    }

    private static void DrawLabel(DrawingContext context, string text, Point origin, Typeface typeface)
    {
        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            11,
            new SolidColorBrush(Color.FromRgb(145, 160, 182)),
            1);
        context.DrawText(formatted, origin);
    }

    private Rect GetPlotRect() =>
        new(45, 18, Math.Max(1, ActualWidth - 63), Math.Max(1, ActualHeight - 53));

    private Point ToScreen(FanCurvePoint point)
    {
        Rect plot = GetPlotRect();
        double x = plot.Left + (point.Temperature - MinTemperature) /
            (MaxTemperature - MinTemperature) * plot.Width;
        double y = plot.Bottom - (point.Duty - MinDuty) / (MaxDuty - MinDuty) * plot.Height;
        return new Point(x, y);
    }

    private static void OnPointsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var editor = (FanCurveEditor)sender;
        editor.Unsubscribe(args.OldValue as IList<FanCurvePoint>);
        editor.Subscribe(args.NewValue as IList<FanCurvePoint>);
        editor.InvalidateVisual();
    }

    private void Subscribe(IList<FanCurvePoint>? points)
    {
        if (points is INotifyCollectionChanged collection)
            collection.CollectionChanged += PointsOnCollectionChanged;
        if (points is not null)
            foreach (FanCurvePoint point in points)
                point.PropertyChanged += PointOnPropertyChanged;
    }

    private void Unsubscribe(IList<FanCurvePoint>? points)
    {
        if (points is INotifyCollectionChanged collection)
            collection.CollectionChanged -= PointsOnCollectionChanged;
        if (points is not null)
            foreach (FanCurvePoint point in points)
                point.PropertyChanged -= PointOnPropertyChanged;
    }

    private void PointsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null)
            foreach (FanCurvePoint point in args.OldItems)
                point.PropertyChanged -= PointOnPropertyChanged;
        if (args.NewItems is not null)
            foreach (FanCurvePoint point in args.NewItems)
                point.PropertyChanged += PointOnPropertyChanged;
        InvalidateVisual();
    }

    private void PointOnPropertyChanged(object? sender, PropertyChangedEventArgs args) => InvalidateVisual();
}
