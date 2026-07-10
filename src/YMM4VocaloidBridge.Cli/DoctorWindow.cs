using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal sealed class DoctorWindow : Window
{
    private DoctorWindow(DoctorResult result)
    {
        Title = "YMM4 Vocaloid Bridge - Doctor";
        Width = 760;
        Height = 460;
        MinWidth = 640;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.White;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "YMM4 Vocaloid Bridge",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(28, 32, 36)),
        };
        root.Children.Add(title);

        var status = new Border
        {
            Margin = new Thickness(0, 12, 0, 16),
            Padding = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush(result.Ready
                ? Color.FromRgb(226, 245, 234)
                : Color.FromRgb(255, 236, 232)),
            BorderBrush = new SolidColorBrush(result.Ready
                ? Color.FromRgb(49, 131, 84)
                : Color.FromRgb(190, 64, 51)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = result.Ready ? "READY - required products were detected" : "NOT READY - resolve the failed checks below",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(result.Ready
                    ? Color.FromRgb(34, 105, 66)
                    : Color.FromRgb(155, 46, 38)),
            },
        };
        Grid.SetRow(status, 1);
        root.Children.Add(status);

        var rows = new List<DoctorRow>
        {
            new(".NET", true, result.Dotnet),
            new("YMM4", result.Ymm4.Ready, result.Ymm4.Version ?? result.Ymm4.Directory ?? "Not detected"),
        };
        rows.AddRange(result.Diagnostics.Select(x => new DoctorRow(x.Code, x.Success, x.Message)));

        var table = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            ItemsSource = rows,
            BorderBrush = new SolidColorBrush(Color.FromRgb(205, 210, 214)),
            BorderThickness = new Thickness(1),
        };
        table.Columns.Add(new DataGridTextColumn { Header = "Check", Binding = new System.Windows.Data.Binding(nameof(DoctorRow.Check)), Width = 150 });
        table.Columns.Add(new DataGridTextColumn { Header = "Result", Binding = new System.Windows.Data.Binding(nameof(DoctorRow.Result)), Width = 90 });
        table.Columns.Add(new DataGridTextColumn { Header = "Details", Binding = new System.Windows.Data.Binding(nameof(DoctorRow.Details)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        Grid.SetRow(table, 2);
        root.Children.Add(table);

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        var openLogs = new Button
        {
            Content = "Open data folder",
            MinWidth = 132,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
        };
        openLogs.Click += (_, _) =>
        {
            Directory.CreateDirectory(result.ApplicationData);
            _ = Process.Start(new ProcessStartInfo("explorer.exe", result.ApplicationData) { UseShellExecute = true });
        };
        var close = new Button { Content = "Close", MinWidth = 88, Height = 32, IsDefault = true };
        close.Click += (_, _) => Close();
        commands.Children.Add(openLogs);
        commands.Children.Add(close);
        Grid.SetRow(commands, 3);
        root.Children.Add(commands);

        Content = root;
    }

    public static void ShowModal(DoctorResult result)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                _ = new DoctorWindow(result).ShowDialog();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new InvalidOperationException("The doctor window could not be opened.", failure);
        }
    }

    private sealed record DoctorRow(string Check, bool Success, string Details)
    {
        public string Result => Success ? "OK" : "MISSING";
    }
}
