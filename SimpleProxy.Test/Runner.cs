using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using NUnit.Framework;

namespace Kontur.Elba.Utilities.Tests.Reflection
{
	[TestFixture]
	public class Runner
	{
		[Test]
		public void Run()
		{
			BenchmarkRunner.Run<Benchmark>(new FastAndDirtyConfig());
		}

		public class FastAndDirtyConfig : ManualConfig
		{
			public FastAndDirtyConfig()
			{
				Add(Job.Default
					.WithLaunchCount(1)     // benchmark process will be launched only once
					.WithIterationTime(100) // 100ms per iteration
					.WithWarmupCount(1)     // 3 warmup iteration
					.WithTargetCount(3)     // 3 target iteration
				);
				Add(HtmlExporter.Default);
				Add(ConsoleLogger.Default);
				Add(PropertyColumn.Type, PropertyColumn.Method, PropertyColumn.LaunchCount, StatisticColumn.Median, StatisticColumn.P95);
			}
		}
	}
}