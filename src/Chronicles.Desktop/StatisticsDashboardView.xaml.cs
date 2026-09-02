using System.Windows;
using System.Windows.Controls;

namespace Chronicles.Desktop;

public sealed record StatisticsMetric(
    string Label,
    long Value,
    string DisplayValue,
    string Note);

public partial class StatisticsDashboardView : UserControl
{
    private IReadOnlyList<StatisticsMetric> _metrics = Array.Empty<StatisticsMetric>();
    private long _revision = long.MinValue;
    private int _renderedTab = -1;
    private long _renderedRevision = long.MinValue;

    public StatisticsDashboardView()
    {
        InitializeComponent();
    }

    public void Apply(
        string title,
        string description,
        IReadOnlyList<StatisticsMetric> metrics,
        long revision)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        TitleText.Text = title;
        DescriptionText.Text = description;
        RevisionText.Text = $"revision {revision:N0}";
        _metrics = metrics;
        _revision = revision;
        RenderSelectedTab();
    }

    public void ResetProjection()
    {
        _metrics = Array.Empty<StatisticsMetric>();
        _revision = long.MinValue;
        _renderedRevision = long.MinValue;
        _renderedTab = -1;
        SummaryItemsControl.ItemsSource = null;
        TableGrid.ItemsSource = null;
        GraphItemsControl.ItemsSource = null;
    }

    private void ViewTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender) && IsInitialized)
        {
            RenderSelectedTab();
        }
    }

    private void RenderSelectedTab()
    {
        var selectedTab = ViewTabControl.SelectedIndex;
        if (_renderedTab == selectedTab && _renderedRevision == _revision)
        {
            return;
        }

        SummaryItemsControl.ItemsSource = null;
        TableGrid.ItemsSource = null;
        GraphItemsControl.ItemsSource = null;
        switch (selectedTab)
        {
            case 0:
                SummaryItemsControl.ItemsSource = _metrics;
                break;
            case 1:
                TableGrid.ItemsSource = _metrics;
                break;
            case 2:
                GraphItemsControl.ItemsSource = CreateGraphRows(_metrics);
                break;
        }

        _renderedTab = selectedTab;
        _renderedRevision = _revision;
    }

    private static StatisticsGraphRow[] CreateGraphRows(
        IReadOnlyList<StatisticsMetric> metrics)
    {
        var maximum = metrics.Count == 0 ? 0L : metrics.Max(metric => Math.Max(0L, metric.Value));
        return metrics.Select(metric => new StatisticsGraphRow(
            metric.Label,
            metric.DisplayValue,
            maximum == 0L
                ? 0d
                : Math.Clamp(metric.Value * 100d / maximum, 0d, 100d)))
            .ToArray();
    }

    private sealed record StatisticsGraphRow(
        string Label,
        string DisplayValue,
        double Ratio);
}
