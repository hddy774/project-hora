using System.Windows;
using System.Windows.Controls;
using Chronicles.Application;

namespace Chronicles.Desktop;

/// <summary>
/// Equal-width wrapping panel for observer summary cards. It avoids nested scroll viewers and
/// keeps card text readable when the window narrows or the requested UI scale grows.
/// </summary>
public sealed class ResponsiveCardPanel : Panel
{
    public static readonly DependencyProperty MinimumCardWidthProperty = DependencyProperty.Register(
        nameof(MinimumCardWidth),
        typeof(double),
        typeof(ResponsiveCardPanel),
        new FrameworkPropertyMetadata(
            150d,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ColumnGapProperty = DependencyProperty.Register(
        nameof(ColumnGap),
        typeof(double),
        typeof(ResponsiveCardPanel),
        new FrameworkPropertyMetadata(
            8d,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap),
        typeof(double),
        typeof(ResponsiveCardPanel),
        new FrameworkPropertyMetadata(
            8d,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty MaximumColumnsProperty = DependencyProperty.Register(
        nameof(MaximumColumns),
        typeof(int),
        typeof(ResponsiveCardPanel),
        new FrameworkPropertyMetadata(
            6,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public double MinimumCardWidth
    {
        get => (double)GetValue(MinimumCardWidthProperty);
        set => SetValue(MinimumCardWidthProperty, value);
    }

    public double ColumnGap
    {
        get => (double)GetValue(ColumnGapProperty);
        set => SetValue(ColumnGapProperty, value);
    }

    public double RowGap
    {
        get => (double)GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    public int MaximumColumns
    {
        get => (int)GetValue(MaximumColumnsProperty);
        set => SetValue(MaximumColumnsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count == 0)
        {
            return new Size(double.IsInfinity(availableSize.Width) ? 0d : availableSize.Width, 0d);
        }

        var width = ResolveWidth(availableSize.Width);
        var columns = GetColumns(width);
        if (columns == 0)
        {
            return new Size(width, 0d);
        }

        var cardWidth = GetCardWidth(width, columns);
        var rowHeights = new double[GetRowCount(columns)];
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            child.Measure(new Size(cardWidth, double.PositiveInfinity));
            rowHeights[index / columns] = Math.Max(
                rowHeights[index / columns],
                child.DesiredSize.Height);
        }

        return new Size(width, rowHeights.Sum() + RowGap * Math.Max(0, rowHeights.Length - 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = GetColumns(finalSize.Width);
        if (columns == 0)
        {
            return finalSize;
        }

        var cardWidth = GetCardWidth(finalSize.Width, columns);
        var rowHeights = new double[GetRowCount(columns)];
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            rowHeights[index / columns] = Math.Max(
                rowHeights[index / columns],
                InternalChildren[index].DesiredSize.Height);
        }

        var y = 0d;
        for (var row = 0; row < rowHeights.Length; row++)
        {
            var rowStart = row * columns;
            var rowCount = Math.Min(columns, InternalChildren.Count - rowStart);
            for (var column = 0; column < rowCount; column++)
            {
                InternalChildren[rowStart + column].Arrange(new Rect(
                    column * (cardWidth + ColumnGap),
                    y,
                    cardWidth,
                    rowHeights[row]));
            }
            y += rowHeights[row] + RowGap;
        }

        return finalSize;
    }

    private int GetColumns(double width) => ResponsiveCardLayoutPolicy.CalculateColumns(
        Math.Max(0d, width),
        MinimumCardWidth,
        ColumnGap,
        InternalChildren.Count,
        MaximumColumns);

    private int GetRowCount(int columns) =>
        (InternalChildren.Count + columns - 1) / columns;

    private double GetCardWidth(double width, int columns) =>
        Math.Max(0d, (width - ColumnGap * (columns - 1)) / columns);

    private double ResolveWidth(double availableWidth)
    {
        if (!double.IsInfinity(availableWidth))
        {
            return Math.Max(0d, availableWidth);
        }

        var columns = Math.Min(MaximumColumns, InternalChildren.Count);
        return MinimumCardWidth * columns + ColumnGap * Math.Max(0, columns - 1);
    }
}
