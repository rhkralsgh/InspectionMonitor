using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Oracle.ManagedDataAccess.Client;

namespace InspectionMonitor;

public partial class MainWindow : Window
{
    private readonly OracleInspectionRepository _repository = new();

    public MainWindow()
    {
        InitializeComponent();

        InspectionDatePicker.SelectedDate = DateTime.Today;
        EquipmentComboBox.SelectedIndex = 0;
        Loaded += async (_, _) => await SearchAsync();
    }

    private void OpenSecom_Click(object sender, RoutedEventArgs e)
    {
        new SecomWindow { Owner = this }.ShowDialog();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        if (InspectionDatePicker.SelectedDate is not DateTime date)
        {
            MessageBox.Show("조회할 날짜를 선택해 주세요.");
            return;
        }

        var selectedItem = EquipmentComboBox.SelectedItem as ComboBoxItem;
        string? equipment = selectedItem?.Content?.ToString();

        if (equipment == "전체")
            equipment = null;

        SearchButton.IsEnabled = false;
        SearchButton.Content = "조회 중";

        try
        {
            var rows = await _repository.FindAsync(date, equipment);
            ShowResults(rows);
        }
        catch (OracleException ex)
        {
            ShowError("Oracle 조회에 실패했습니다.", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            ShowError("DB 연결 설정을 확인해 주세요.", ex.Message);
        }
        finally
        {
            SearchButton.Content = "조회";
            SearchButton.IsEnabled = true;
        }
    }

    private void ShowResults(IReadOnlyList<InspectionResult> rows)
    {
        var (total, byEquipment) = InspectionAnalysis.Analyze(rows);

        ResultGrid.ItemsSource = rows;
        EquipmentChart.ItemsSource = byEquipment;

        TotalCountText.Text = total.TotalCount.ToString();
        NgCountText.Text = total.NgCount.ToString();
        NgRateText.Text = $"{total.NgRate:F1}%";

        ChartEmptyText.Text = "조회 결과가 없습니다.";
        ChartEmptyText.Visibility = rows.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ShowError(string status, string detail)
    {
        ShowResults(Array.Empty<InspectionResult>());
        ChartEmptyText.Text = status;
        MessageBox.Show(this, detail, status);
    }
}