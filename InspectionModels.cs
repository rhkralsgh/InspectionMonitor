using System;

namespace InspectionMonitor
{
    public class InspectionResult
    {
        public int Id { get; set; }
        public DateTime InspectedAt { get; set; }
        public string Equipment { get; set; } = string.Empty;
        public double Value { get; set; }
        public bool IsNg => Value < 5.0 || Value > 6.0;
        public string Status => IsNg ? "NG" : "OK";
        public string DisplayTime =>
            InspectedAt.ToString("MM-dd HH:mm");
        public string DisplayValue =>
            Value.ToString("F1");
    }

    public class EquipmentSummary
    {
        public string Equipment { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int NgCount { get; set; }
        public double NgRate =>
            TotalCount == 0 ? 0 : NgCount * 100.0 / TotalCount;
        public double BarWidth => NgRate * 4.0;
        public string DisplayText =>
            $"{NgRate:F1}% (NG {NgCount}/{TotalCount})";
    }
}