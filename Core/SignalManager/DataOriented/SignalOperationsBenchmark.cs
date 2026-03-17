using System;
using System.Diagnostics;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.DataOriented
{
    /// <summary>
    /// Benchmark utilities for comparing SIMD vs scalar performance
    /// Provides detailed timing metrics for optimization analysis
    /// </summary>
    public static class SignalOperationsBenchmark
    {
        private const int WARMUP_ITERATIONS = 5;
        private const int BENCHMARK_ITERATIONS = 100;
        
        /// <summary>
        /// Runs comprehensive benchmark suite comparing SIMD vs Scalar implementations
        /// </summary>
        public static BenchmarkResults RunFullBenchmark(int tableSize = 1000)
        {
            Console.WriteLine($"\n[BENCHMARK] Starting comprehensive benchmark (table size: {tableSize})...");
            Console.WriteLine($"[BENCHMARK] Warmup: {WARMUP_ITERATIONS} iterations, Benchmark: {BENCHMARK_ITERATIONS} iterations\n");
            
            var table = CreateTestTable(tableSize);
            var results = new BenchmarkResults { TableSize = tableSize };
            
            // Warmup
            WarmupBenchmarks(table);
            
            // Benchmark 1: ValidateTiming
            results.ValidateTimingScalar = BenchmarkScalarValidateTiming(table);
            results.ValidateTimingSIMD = BenchmarkSIMDValidateTiming(table);
            results.ValidateTimingSpeedup = results.ValidateTimingScalar / results.ValidateTimingSIMD;
            
            // Benchmark 2: ValidateChannels
            results.ValidateChannelsScalar = BenchmarkScalarValidateChannels(table);
            results.ValidateChannelsSIMD = BenchmarkSIMDValidateChannels(table);
            results.ValidateChannelsSpeedup = results.ValidateChannelsScalar / results.ValidateChannelsSIMD;
            
            // Benchmark 3: CalculateTotalDuration
            results.CalcDurationScalar = BenchmarkScalarCalculateDuration(table);
            results.CalcDurationSIMD = BenchmarkSIMDCalculateDuration(table);
            results.CalcDurationSpeedup = results.CalcDurationScalar / results.CalcDurationSIMD;
            
            // Benchmark 4: FilterByChannel
            results.FilterChannelScalar = BenchmarkScalarFilterChannel(table);
            results.FilterChannelSIMD = BenchmarkSIMDFilterChannel(table);
            results.FilterChannelSpeedup = results.FilterChannelScalar / results.FilterChannelSIMD;
            
            // Benchmark 5: ValidateAll (full validation)
            results.ValidateAllScalar = BenchmarkScalarValidateAll(table);
            results.ValidateAllSIMD = BenchmarkSIMDValidateAll(table);
            results.ValidateAllSpeedup = results.ValidateAllScalar / results.ValidateAllSIMD;
            
            Console.WriteLine($"\n[BENCHMARK] Benchmark complete!\n");
            return results;
        }
        
        private static void WarmupBenchmarks(SignalTable table)
        {
            Console.WriteLine("[BENCHMARK] Warming up JIT compiler...");
            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                // Warmup scalar
                ScalarValidateTiming(table);
                ScalarValidateChannels(table);
                ScalarCalculateDuration(table);
                ScalarFilterChannel(table, 0);
                SignalOperations.ValidateAll(table);
                
                // Warmup SIMD
                SignalOperationsSIMD.ValidateTimingBatch(table);
                SignalOperationsSIMD.ValidateChannelsBatch(table);
                SignalOperationsSIMD.CalculateTotalDurationSIMD(table);
                SignalOperationsSIMD.FilterByChannelSIMD(table, 0);
                SignalOperationsSIMD.ValidateAllSIMD(table);
            }
            Console.WriteLine("[BENCHMARK] Warmup complete\n");
        }
        
        // ========== SCALAR IMPLEMENTATIONS FOR COMPARISON ==========
        
        private static bool[] ScalarValidateTiming(SignalTable table)
        {
            var results = new bool[table.Count];
            for (int i = 0; i < table.Count; i++)
            {
                results[i] = table.StartTimesNs[i] >= 0 && table.DurationsNs[i] > 0;
            }
            return results;
        }
        
        private static bool[] ScalarValidateChannels(SignalTable table)
        {
            var results = new bool[table.Count];
            for (int i = 0; i < table.Count; i++)
            {
                results[i] = table.Channels[i] >= 0 && table.Channels[i] <= 31;
            }
            return results;
        }
        
        private static long ScalarCalculateDuration(SignalTable table)
        {
            long maxEndTime = 0;
            for (int i = 0; i < table.Count; i++)
            {
                long endTime = table.StartTimesNs[i] + table.DurationsNs[i];
                if (endTime > maxEndTime)
                    maxEndTime = endTime;
            }
            return maxEndTime;
        }
        
        private static int[] ScalarFilterChannel(SignalTable table, int targetChannel)
        {
            var matches = new System.Collections.Generic.List<int>();
            for (int i = 0; i < table.Count; i++)
            {
                if (table.Channels[i] == targetChannel)
                    matches.Add(i);
            }
            return matches.ToArray();
        }
        
        // ========== BENCHMARK METHODS ==========
        
        private static double BenchmarkScalarValidateTiming(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = ScalarValidateTiming(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SCALAR] ValidateTiming:      {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkSIMDValidateTiming(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = SignalOperationsSIMD.ValidateTimingBatch(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SIMD]   ValidateTiming:      {avgMs:F6} ms/iter → Speedup: {BenchmarkScalarValidateTiming(table) / avgMs:F2}x");
            return avgMs;
        }
        
        private static double BenchmarkScalarValidateChannels(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = ScalarValidateChannels(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SCALAR] ValidateChannels:    {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkSIMDValidateChannels(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = SignalOperationsSIMD.ValidateChannelsBatch(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SIMD]   ValidateChannels:    {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkScalarCalculateDuration(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = ScalarCalculateDuration(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SCALAR] CalcTotalDuration:   {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkSIMDCalculateDuration(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = SignalOperationsSIMD.CalculateTotalDurationSIMD(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SIMD]   CalcTotalDuration:   {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkScalarFilterChannel(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = ScalarFilterChannel(table, 0);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SCALAR] FilterByChannel:     {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkSIMDFilterChannel(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = SignalOperationsSIMD.FilterByChannelSIMD(table, 0);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SIMD]   FilterByChannel:     {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkScalarValidateAll(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = SignalOperations.ValidateAll(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SCALAR] ValidateAll:         {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        private static double BenchmarkSIMDValidateAll(SignalTable table)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BENCHMARK_ITERATIONS; i++)
            {
                var result = SignalOperationsSIMD.ValidateAllSIMD(table);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / BENCHMARK_ITERATIONS;
            Console.WriteLine($"[SIMD]   ValidateAll:         {avgMs:F6} ms/iter");
            return avgMs;
        }
        
        // ========== TEST DATA GENERATION ==========
        
        private static SignalTable CreateTestTable(int size)
        {
            Console.WriteLine($"[BENCHMARK] Creating test table with {size} events...");
            var table = new SignalTable(size);
            var random = new Random(42); // Fixed seed for reproducibility
            
            for (int i = 0; i < size; i++)
            {
                table.AddSignal(
                    name: $"Event_{i}",
                    startTimeNs: (long)(random.NextDouble() * 10_000_000_000), // 0-10s
                    durationNs: (long)(random.NextDouble() * 1_000_000_000),   // 0-1s
                    channel: random.Next(0, 32),
                    deviceType: i % 2 == 0 ? DeviceType.Analog : DeviceType.Digital,
                    deviceModel: i % 2 == 0 ? "PCIe-1824" : "PCI-1735U",
                    eventType: (SignalEventType)(random.Next(0, 4)),
                    color: "#2ECC71"
                );
                
                // Add attributes for Ramp events
                if (table.EventTypes[i] == SignalEventType.Ramp)
                {
                    table.Attributes.SetStartVoltage(i, random.NextDouble() * 10);
                    table.Attributes.SetEndVoltage(i, random.NextDouble() * 10);
                }
                else if (table.EventTypes[i] == SignalEventType.DC)
                {
                    table.Attributes.SetVoltage(i, random.NextDouble() * 10);
                }
                else if (table.EventTypes[i] == SignalEventType.Waveform)
                {
                    table.Attributes.SetWaveformParams(i, 
                        random.NextDouble() * 1000,  // freq
                        random.NextDouble() * 5,     // amp
                        random.NextDouble() * 5);    // offset
                }
            }
            
            Console.WriteLine($"[BENCHMARK] Test table created successfully\n");
            return table;
        }
    }
    
    /// <summary>
    /// Container for benchmark results
    /// </summary>
    public class BenchmarkResults
    {
        public int TableSize { get; set; }
        
        // ValidateTiming
        public double ValidateTimingScalar { get; set; }
        public double ValidateTimingSIMD { get; set; }
        public double ValidateTimingSpeedup { get; set; }
        
        // ValidateChannels
        public double ValidateChannelsScalar { get; set; }
        public double ValidateChannelsSIMD { get; set; }
        public double ValidateChannelsSpeedup { get; set; }
        
        // CalculateTotalDuration
        public double CalcDurationScalar { get; set; }
        public double CalcDurationSIMD { get; set; }
        public double CalcDurationSpeedup { get; set; }
        
        // FilterByChannel
        public double FilterChannelScalar { get; set; }
        public double FilterChannelSIMD { get; set; }
        public double FilterChannelSpeedup { get; set; }
        
        // ValidateAll
        public double ValidateAllScalar { get; set; }
        public double ValidateAllSIMD { get; set; }
        public double ValidateAllSpeedup { get; set; }
        
        public void PrintSummary()
        {
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("BENCHMARK SUMMARY");
            Console.WriteLine(new string('=', 70));
            Console.WriteLine($"Table Size: {TableSize} events\n");
            
            Console.WriteLine($"{"Operation",-25} {"Scalar (ms)",-15} {"SIMD (ms)",-15} {"Speedup",-10}");
            Console.WriteLine(new string('-', 70));
            Console.WriteLine($"{"ValidateTiming",-25} {ValidateTimingScalar,-15:F6} {ValidateTimingSIMD,-15:F6} {ValidateTimingSpeedup,-10:F2}x");
            Console.WriteLine($"{"ValidateChannels",-25} {ValidateChannelsScalar,-15:F6} {ValidateChannelsSIMD,-15:F6} {ValidateChannelsSpeedup,-10:F2}x");
            Console.WriteLine($"{"CalcTotalDuration",-25} {CalcDurationScalar,-15:F6} {CalcDurationSIMD,-15:F6} {CalcDurationSpeedup,-10:F2}x");
            Console.WriteLine($"{"FilterByChannel",-25} {FilterChannelScalar,-15:F6} {FilterChannelSIMD,-15:F6} {FilterChannelSpeedup,-10:F2}x");
            Console.WriteLine($"{"ValidateAll",-25} {ValidateAllScalar,-15:F6} {ValidateAllSIMD,-15:F6} {ValidateAllSpeedup,-10:F2}x");
            Console.WriteLine(new string('=', 70));
            
            double avgSpeedup = (ValidateTimingSpeedup + ValidateChannelsSpeedup + CalcDurationSpeedup + 
                                FilterChannelSpeedup + ValidateAllSpeedup) / 5.0;
            Console.WriteLine($"Average SIMD Speedup: {avgSpeedup:F2}x");
            Console.WriteLine(new string('=', 70) + "\n");
        }
    }
}
