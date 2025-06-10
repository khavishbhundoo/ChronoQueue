// See https://aka.ms/new-console-template for more information

using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using Perfolizer.Metrology;

var summaryStyle = new SummaryStyle(
    cultureInfo: CultureInfo.InvariantCulture,
    printUnitsInHeader: true,
    sizeUnit: SizeUnit.GetBestSizeUnit(),
    timeUnit: TimeUnit.GetBestTimeUnit(),
    printUnitsInContent: false,
    printZeroValuesInContent: true,
    maxParameterColumnWidth: 20,
    ratioStyle: RatioStyle.Value,
    textColumnJustification: SummaryTable.SummaryTableColumn.TextJustification.Left,
    numericColumnJustification: SummaryTable.SummaryTableColumn.TextJustification.Right
);

var config = ManualConfig.Create(DefaultConfig.Instance)
    .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
    .WithSummaryStyle(summaryStyle)
    .AddExporter(JsonExporter.Default)
    .AddExporter(MarkdownExporter.GitHub); // Export as GitHub markdown

BenchmarkRunner.Run(typeof(Program).Assembly, config);

