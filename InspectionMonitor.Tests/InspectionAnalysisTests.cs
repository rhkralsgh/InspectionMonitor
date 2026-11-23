using System;
using System.IO;
using InspectionMonitor;
using Xunit;

namespace InspectionMonitor.Tests;

public sealed class InspectionAnalysisTests
{
    [Theory]
    [InlineData(4.99, true)]
    [InlineData(5.00, false)]
    [InlineData(6.00, false)]
    [InlineData(6.01, true)]
    public void IsNg_UsesInclusiveLimits(double measuredValue, bool expectedNg)
    {
        var result = new InspectionResult { Value = measuredValue };

        Assert.Equal(expectedNg, result.IsNg);
    }

    [Fact]
    public void Analyze_EmptyResults_ReturnsZeroAndNoEquipment()
    {
        var (total, byEquipment) = InspectionAnalysis.Analyze(Array.Empty<InspectionResult>());

        Assert.Equal(0, total.TotalCount);
        Assert.Equal(0, total.NgCount);
        Assert.Equal(0.0, total.NgRate);
        Assert.Empty(byEquipment);
    }

    [Fact]
    public void Analyze_MixedEquipment_CalculatesRatesPerEquipment()
    {
        InspectionResult[] rows =
        [
            new() { Equipment = "설비 A", Value = 5.4 },
            new() { Equipment = "설비 A", Value = 4.8 },
            new() { Equipment = "설비 B", Value = 5.5 },
            new() { Equipment = "설비 B", Value = 5.8 }
        ];

        var (total, byEquipment) = InspectionAnalysis.Analyze(rows);

        Assert.Equal(4, total.TotalCount);
        Assert.Equal(1, total.NgCount);
        Assert.Equal(25.0, total.NgRate);

        Assert.Collection(byEquipment,
            a =>
            {
                Assert.Equal("설비 A", a.Equipment);
                Assert.Equal(50.0, a.NgRate);
            },
            b =>
            {
                Assert.Equal("설비 B", b.Equipment);
                Assert.Equal(0.0, b.NgRate);
            });
    }
}

public sealed class SecomAnalysisTests
{
    [Fact]
    public void ParseLabels_PreservesOutcomesAndRepeatedTimestamps()
    {
        var rows = SecomAnalysis.ParseLabels(
        [
            "-1 \"05/08/2008 07:12:00\"",
            "1 \"05/08/2008 07:12:00\""
        ]);

        Assert.Collection(rows,
            pass =>
            {
                Assert.Equal(1, pass.Id);
                Assert.Equal(new DateTime(2008, 8, 5, 7, 12, 0), pass.InspectedAt);
                Assert.Equal("OK", pass.Status);
            },
            fail =>
            {
                Assert.Equal(2, fail.Id);
                Assert.Equal(rows[0].InspectedAt, fail.InspectedAt);
                Assert.Equal("NG", fail.Status);
            });
    }

    [Theory]
    [InlineData("0 \"19/07/2008 11:55:00\"")]
    [InlineData("-1 \"31/02/2008 11:55:00\"")]
    public void ParseLabels_InvalidRow_ReportsLineNumber(string invalidLine)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            SecomAnalysis.ParseLabels(["-1 \"19/07/2008 11:55:00\"", invalidLine]));

        Assert.Contains("2번째 줄", error.Message);
    }

    [Fact]
    public void GroupByWeek_SplitsSundayAndMonday()
    {
        SecomSample[] rows =
        [
            new(1, new DateTime(2008, 7, 20, 10, 0, 0), -1),
            new(2, new DateTime(2008, 7, 20, 11, 0, 0), 1),
            new(3, new DateTime(2008, 7, 21, 9, 0, 0), -1)
        ];

        var weeks = SecomAnalysis.GroupByWeek(rows);

        Assert.Collection(weeks,
            first =>
            {
                Assert.Equal(new DateTime(2008, 7, 14), first.WeekStart);
                Assert.Equal(2, first.TotalCount);
                Assert.Equal(1, first.NgCount);
                Assert.Equal(50.0, first.Rate);
            },
            second =>
            {
                Assert.Equal(new DateTime(2008, 7, 21), second.WeekStart);
                Assert.Equal(1, second.TotalCount);
                Assert.Equal(0, second.NgCount);
                Assert.Equal(0.0, second.Rate);
            });
    }
}
