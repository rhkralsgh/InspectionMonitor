using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Oracle.ManagedDataAccess.Client;

namespace InspectionMonitor;

public partial class SecomWindow : Window
{
    private readonly OracleSecomRepository _repository = new();
    private List<SecomSample> _rows = new();

    public SecomWindow()
    {
        InitializeComponent();
        FromDatePicker.SelectedDate = new DateTime(2008, 7, 19);
        ToDatePicker.SelectedDate = new DateTime(2008, 10, 17);
        Loaded += async (_, _) => await SearchAsync();
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "SECOM 판정 파일 선택",
            Filter = "SECOM 판정 파일 (*.data)|*.data|모든 파일 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        SetBusy(true);
        StatusText.Text = "판정 파일을 확인하고 있습니다.";

        try
        {
            var rows = SecomAnalysis.ParseLabels(await File.ReadAllLinesAsync(dialog.FileName));

            if (rows.Count != 1567 || rows.Count(row => row.IsNg) != 104)
                throw new InvalidDataException(
                    "원본 secom_labels.data 파일이 맞는지 확인해 주세요. 예상값은 1,567건과 NG 104건입니다.");

            await _repository.ImportAsync(rows);
            FromDatePicker.SelectedDate = rows.Min(row => row.InspectedAt).Date;
            ToDatePicker.SelectedDate = rows.Max(row => row.InspectedAt).Date;

            await DisplayPeriodAsync(FromDatePicker.SelectedDate.Value,
                ToDatePicker.SelectedDate.Value, "판정 파일 적재 완료.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                       or OracleException or InvalidOperationException)
        {
            StatusText.Text = $"가져오기 작업 확인 · {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        if (FromDatePicker.SelectedDate is not DateTime from ||
            ToDatePicker.SelectedDate is not DateTime through ||
            from.Date > through.Date || through.Date == DateTime.MaxValue.Date)
        {
            StatusText.Text = "시작일과 종료일을 확인해 주세요.";
            return;
        }

        SetBusy(true);
        StatusText.Text = "Oracle에서 검사 기록을 읽고 있습니다.";

        try
        {
            await DisplayPeriodAsync(from, through);
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            StatusText.Text = "판정 파일 가져오기를 눌러 secom_labels.data를 먼저 선택해 주세요.";
        }
        catch (Exception ex) when (ex is OracleException or InvalidOperationException)
        {
            StatusText.Text = $"조회 확인 · {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DisplayPeriodAsync(DateTime from, DateTime through, string? message = null)
    {
        _rows = await _repository.FindAsync(from, through);
        int ngCount = _rows.Count(row => row.IsNg);
        double rate = _rows.Count == 0 ? 0 : ngCount * 100.0 / _rows.Count;

        SummaryText.Text =
            $"전체 {_rows.Count:N0}건  |  NG {ngCount:N0}건  |  불합격률 {rate:F1}%";
        WeekChart.ItemsSource = SecomAnalysis.GroupByWeek(_rows);
        ApplyTableFilter();

        StatusText.Text = _rows.Count == 0
            ? "선택한 기간에 검사 기록이 없습니다."
            : $"{from:yyyy-MM-dd} ~ {through:yyyy-MM-dd} 조회 완료. {message}".TrimEnd();
    }

    private void NgFilter_Changed(object sender, RoutedEventArgs e)
    {
        ApplyTableFilter();
    }

    private void ApplyTableFilter()
    {
        if (SecomResultGrid is null)
            return;

        SecomResultGrid.ItemsSource = NgOnlyCheckBox.IsChecked == true
            ? _rows.Where(row => row.IsNg).ToList()
            : _rows;
    }

    private void SetBusy(bool busy)
    {
        SecomSearchButton.IsEnabled = !busy;
        ImportButton.IsEnabled = !busy;
        FromDatePicker.IsEnabled = !busy;
        ToDatePicker.IsEnabled = !busy;
    }
}
