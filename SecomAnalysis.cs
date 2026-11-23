using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace InspectionMonitor;

public sealed record SecomSample(int Id, DateTime InspectedAt, int Label)
{
    public bool IsNg => Label == 1;
    public string Status => IsNg ? "NG" : "OK";
}

public sealed record SecomWeek(DateTime WeekStart, int TotalCount, int NgCount)
{
    public double Rate => NgCount * 100.0 / TotalCount;
    public double BarWidth => Rate * 2.4;
    public string Counts => $"{Rate:F1}% ({NgCount}/{TotalCount})";
}

public static class SecomAnalysis
{
    public static List<SecomSample> ParseLabels(IEnumerable<string> lines)
    {
        var samples = new List<SecomSample>();

        foreach (var line in lines)
        {
            var fields = line.Trim().Split('"');
            var lineNumber = samples.Count + 1;

            if (fields.Length != 3 || fields[2].Length != 0 ||
                !int.TryParse(fields[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var label) ||
                (label != -1 && label != 1) ||
                !DateTime.TryParseExact(fields[1], "dd/MM/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var inspectedAt))
            {
                throw new InvalidDataException(
                    $"{lineNumber}번째 줄의 판정 또는 검사 시각 형식을 확인해 주세요.");
            }

            // 같은 검사 시각이 나올 수 있으므로 원본 줄 번호를 키로 사용합니다.
            samples.Add(new SecomSample(lineNumber, inspectedAt, label));
        }

        return samples;
    }

    public static List<SecomWeek> GroupByWeek(IEnumerable<SecomSample> samples)
    {
        return samples
            .GroupBy(sample =>
            {
                DateTime date = sample.InspectedAt.Date;
                int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
                return date.AddDays(-daysSinceMonday);
            })
            .OrderBy(group => group.Key)
            .Select(group => new SecomWeek(
                group.Key, group.Count(), group.Count(sample => sample.IsNg)))
            .ToList();
    }
}
