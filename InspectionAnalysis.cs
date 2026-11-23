using System.Collections.Generic;
using System.Linq;

namespace InspectionMonitor;

public static class InspectionAnalysis
{
    public static (EquipmentSummary Total, List<EquipmentSummary> ByEquipment)
        Analyze(IReadOnlyList<InspectionResult> rows)
    {
        var total = new EquipmentSummary
        {
            Equipment = "전체",
            TotalCount = rows.Count,
            NgCount = rows.Count(row => row.IsNg)
        };

        var byEquipment = rows
            .GroupBy(row => row.Equipment)
            .Select(group => new EquipmentSummary
            {
                Equipment = group.Key,
                TotalCount = group.Count(),
                NgCount = group.Count(row => row.IsNg)
            })
            .OrderBy(summary => summary.Equipment)
            .ToList();

        return (total, byEquipment);
    }
}