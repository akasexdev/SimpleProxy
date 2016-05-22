using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Kontur.Elba.Core.Utilities.Reflection;
using NUnit.Framework;
using NUnit.Framework.Internal;

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

		[Test]
		public void TrashCastle()
		{
			var bench = new Benchmark();
			for (int i = 0; i < 100000; i++)
			{
				bench.Castle_NoParams();
			}
		}

		[Test]
		public void TrashSimple()
		{
			var bench = new Benchmark();
			for (int i = 0; i < 100000; i++)
			{
				bench.Simple_NoParams();
			}
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
				Add(new ConsoleLogger());
			}
		}
	}
}