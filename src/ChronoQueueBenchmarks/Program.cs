// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Running;

var config = ManualConfig.Create(DefaultConfig.Instance)
    .WithOption(ConfigOptions.DisableOptimizationsValidator, true)
    .AddExporter(MarkdownExporter.GitHub);  // Exports results as GitHub-flavored Markdown

BenchmarkRunner.Run(typeof(Program).Assembly, config);

