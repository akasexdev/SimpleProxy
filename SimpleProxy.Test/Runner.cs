using System;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using NUnit.Framework;

namespace SimpleProxy.Test
{
	[TestFixture]
	public class Runner
	{
		[Test]
		public void Run()
		{
			var summary = BenchmarkRunner.Run<Benchmark>(new Config());
			if (summary.ValidationErrors.Any())
				Assert.Fail("Validation errors found: {0}", summary.ValidationErrors.Select(c => c.Message).JoinStrings(Environment.NewLine));
		}

		public class Config : ManualConfig
		{
			public Config()
			{
				Add(Job.Default
					.WithLaunchCount(3)
					.WithIterationTime(300)
					.WithWarmupCount(1)
					.WithTargetCount(3)
				);
				Add(HtmlExporter.Default);
				Add(ConsoleLogger.Default);
				Add(PropertyColumn.Type, PropertyColumn.Method, PropertyColumn.LaunchCount, StatisticColumn.Median, StatisticColumn.P95);
				Add(ExecutionValidator.FailOnError);
				Add(JitOptimizationsValidator.FailOnError);
			}
		}
	}
}