using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace DemoGCProgram
{
    // ------------------------------------------------------------------------
    // 1. SETTINGS - tryby sterowane zmiennymi środowiskowymi
    // ------------------------------------------------------------------------
    static class Settings
    {
        public static int CustomCollect =>
            int.TryParse(Environment.GetEnvironmentVariable("ztp_custom_collect"), out var val) ? val : -1;
        public static bool UsePooling =>
            Environment.GetEnvironmentVariable("ztp_pooling") == "1";
        public static int UnmanagedMode =>
            int.TryParse(Environment.GetEnvironmentVariable("ztp_unmanaged"), out var val) ? val : 0;
        public static bool UseDispose =>
            Environment.GetEnvironmentVariable("ztp_dispose") == "1";
        public static int? ParallelLoopDegree =>
            int.TryParse(Environment.GetEnvironmentVariable("ztp_parallel_loop"), out var val) ? val : (int?)null;
        public static bool UseTasks =>
            Environment.GetEnvironmentVariable("ztp_tasks") == "1";
        public static bool UseSimd =>
            Environment.GetEnvironmentVariable("ztp_simd") == "1";
    }

    // ------------------------------------------------------------------------
    // 2. Klasa bitmapy niezarządzanej z IDisposable
    // ------------------------------------------------------------------------
    public class UnmanagedBitmap : IDisposable
    {
        public IntPtr Buffer { get; private set; }
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        private bool _disposed;

        public UnmanagedBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Stride = width; // zakładamy 1 bajt/piksel
            Buffer = Marshal.AllocHGlobal(width * height);
        }

        public void Fill(byte value)
        {
            unsafe
            {
                var ptr = (byte*)Buffer;
                for (int i = 0; i < Width * Height; i++)
                    ptr[i] = value;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (Buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Buffer);
                    Buffer = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~UnmanagedBitmap() => Dispose(false);
    }

    class Program
    {
        static void Main(string[] args)
        {
            bool exit = false;
            while (!exit)
            {
                Console.WriteLine("\n==== MENU ====");
                Console.WriteLine("1. Mnożenie macierzy dużej przez macierz małą");
                Console.WriteLine("2. Przetwarzanie bitmapy (managed/unmanaged/pooling/GC)");
                Console.WriteLine("3. Analiza pamięci (symulacja, narzędzia zewn.)");
                Console.WriteLine("4. GC: ustawienia, wymuszenie, kompaktacja LOH");
                Console.WriteLine("5. Techniki optymalizacji (pooling, fixed, LowLatency, parallel, SIMD)");
                Console.WriteLine("0. Wyjście");
                Console.Write("Wybierz opcję (0-5): ");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        ExecuteAction("Mnożenie macierzy", MatrixMultiplicationDemo);
                        break;
                    case "2":
                        ExecuteAction("Przetwarzanie bitmapy", ProcessBitmapDemo);
                        break;
                    case "3":
                        ExecuteAction("Analiza pamięci (instrukcja)", MemoryAnalysisDemo);
                        break;
                    case "4":
                        ExecuteAction("GC: konfiguracja i wymuszenie", GCDemo);
                        break;
                    case "5":
                        ExecuteAction("Techniki optymalizacji", OptimizationDemo);
                        break;
                    case "6":
                        ExecuteAction("Wykorzystanie CPU (Systemy wielordzeniowe i wieloprocesorowe)", CpuAffinityDemo);
                        break;
                    case "7":
                        ExecuteAction("Wykorzystanie CPU (pojedynczy rdzeń)", SingleCoreOptimizationDemo);
                        break;
                    case "0":
                        exit = true;
                        Console.WriteLine("Koniec programu.");
                        break;
                    default:
                        Console.WriteLine("Nieprawidłowy wybór. Spróbuj ponownie.");
                        break;
                }
            }
        }

        // wrapper z pomiarem czasu
        static void ExecuteAction(string name, Action action)
        {
            Console.WriteLine($"\n--- {name} ---");
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1; i++)
                action();
            sw.Stop();
            Console.WriteLine($"{name} zakończone w: {sw.ElapsedMilliseconds} ms");
        }

        // --------------------------------------------------------------------
        // 1. Mnożenie macierzy 30000×200 * 200×5
        // --------------------------------------------------------------------
        static void MatrixMultiplicationDemo()
        {
            int rowsA = 30000, colsA = 200, colsB = 5;
            var A = new double[rowsA, colsA];
            var B = new double[colsA, colsB];
            var C = new double[rowsA, colsB];
            var rnd = new Random(42);

            for (int i = 0; i < rowsA; i++)
                for (int j = 0; j < colsA; j++)
                    A[i, j] = rnd.NextDouble();
            for (int i = 0; i < colsA; i++)
                for (int j = 0; j < colsB; j++)
                    B[i, j] = rnd.NextDouble();

            for (int i = 0; i < rowsA; i++)
                for (int j = 0; j < colsB; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < colsA; k++)
                        sum += A[i, k] * B[k, j];
                    C[i, j] = sum;
                }

            Console.WriteLine("Mnożenie macierzy wykonane.");
        }

        // --------------------------------------------------------------------
        // 2. Przetwarzanie bitmapy w różnych trybach
        // --------------------------------------------------------------------
        static void ProcessBitmapDemo()
        {
            const int width = 512, height = 512;

            // wybór trybu managed vs unmanaged
            if (Settings.UnmanagedMode == 1)
            {
                using var bm = new UnmanagedBitmap(width, height);
                bm.Fill(128);
                Console.WriteLine("UnmanagedBitmap (mode=1) wypełnione.");
            }
            else if (Settings.UnmanagedMode == 2)
            {
                // inny wariant unmanaged (np. byte[] + ptr)
                using var bm = new UnmanagedBitmap(width, height);
                bm.Fill(64);
                Console.WriteLine("UnmanagedBitmap (mode=2) wypełnione.");
            }
            else
            {
                // managed byte[,]
                var managed = new byte[height, width];
                if (Settings.UsePooling)
                {
                    // fallback: pooling dla 1D
                    var temp = ArrayPool<byte>.Shared.Rent(width * height);
                    for (int i = 0; i < temp.Length; i++) temp[i] = 128;
                    ArrayPool<byte>.Shared.Return(temp);
                    Console.WriteLine("Managed bitmap + pooling użyte do symulacji fill().");
                }
                else
                {
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            managed[y, x] = 128;
                    Console.WriteLine("Managed bitmap wypełniona (byte[,]).");
                }
            }

            // ewentualne wymuszenie GC
            if (Settings.CustomCollect >= 0)
            {
                GC.Collect(Settings.CustomCollect);
                GC.WaitForPendingFinalizers();
                Console.WriteLine($"Wymuszono GC.Collect({Settings.CustomCollect}).");
            }
        }

        // --------------------------------------------------------------------
        // 3. Symulacja analizy pamięci (instrukcja)
        // --------------------------------------------------------------------
        static void MemoryAnalysisDemo()
        {
            Console.WriteLine("Uruchom DotMemory/PerfView/BenchmarkDotNet z odpowiednimi env vars, aby zebrać dane.");
        }

        // --------------------------------------------------------------------
        // 4. GC demo: LatencyMode, LOH compact, GC.Collect
        // --------------------------------------------------------------------
        static void GCDemo()
        {
            Console.WriteLine("GC Demo:");
            Console.WriteLine($"  LatencyMode przed: {GCSettings.LatencyMode}");
            GCSettings.LatencyMode = GCLatencyMode.LowLatency;
            Console.WriteLine($"  LatencyMode po ustawieniu LowLatency: {GCSettings.LatencyMode}");

            long before = GC.GetTotalMemory(false);
            Console.WriteLine($"  Pamięć przed alokacjami: {before} bajtów");

            // alokacje LOH
            var list = new byte[1000][];
            for (int i = 0; i < list.Length; i++)
                list[i] = new byte[85_000]; // LOH

            // kompaktacja LOH
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            Console.WriteLine("  Wymuszono kompaktację LOH + GC.Collect(2, Forced, compacting)");

            long after = GC.GetTotalMemory(true);
            Console.WriteLine($"  Pamięć po GC: {after} bajtów");
        }

        // --------------------------------------------------------------------
        // 5. Techniki optymalizacji: pooling, fixed, parallel, tasks, SIMD
        // --------------------------------------------------------------------
        static void OptimizationDemo()
        {
            const int N = 10_000_000;
            Console.WriteLine("Optymalizacje:");

            // a) Pooling
            if (Settings.UsePooling)
            {
                var pool = ArrayPool<byte>.Shared;
                var buf = pool.Rent(N);
                for (int i = 0; i < N; i++) buf[i] = (byte)(i % 256);
                pool.Return(buf);
                Console.WriteLine("  Pooling: bufor rented/returned.");
            }

            // b) fixed
            {
                var arr = new byte[1024];
                unsafe
                {
                    fixed (byte* p = arr)
                        for (int i = 0; i < arr.Length; i++) p[i] = 1;
                }
                Console.WriteLine("  fixed: przypięto tablicę w pamięci.");
            }

            // c) LowLatencyMode
            var prev = GCSettings.LatencyMode;
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            Thread.Sleep(50);
            GCSettings.LatencyMode = prev;
            Console.WriteLine("  LowLatencyMode: krótko ustawiono SustainedLowLatency.");

            // d) parallel loop
            if (Settings.ParallelLoopDegree.HasValue)
            {
                Parallel.For(0, N / 1000, new ParallelOptions { MaxDegreeOfParallelism = Settings.ParallelLoopDegree.Value }, i =>
                {
                    // dummy work
                    var x = i * i;
                });
                Console.WriteLine($"  Parallel.For z degree={Settings.ParallelLoopDegree}");
            }

            // e) Tasks
            if (Settings.UseTasks)
            {
                var tasks = new Task[4];
                for (int t = 0; t < 4; t++)
                    tasks[t] = Task.Run(() =>
                    {
                        for (int i = 0; i < N / 4000; i++) { var x = i * i; }
                    });
                Task.WaitAll(tasks);
                Console.WriteLine("  Tasks: zakończono 4 zadania równoległe.");
            }

            // f) SIMD
            if (Settings.UseSimd && Vector.IsHardwareAccelerated)
            {
                int M = Vector<float>.Count;
                var v1 = new Vector<float>(1.0f);
                var v2 = new Vector<float>(2.0f);
                var sum = v1 + v2;
                Console.WriteLine($"  SIMD: Vector<float>.Count = {M}, wynik sumy[0]={sum[0]}.");
            }
            else if (Settings.UseSimd)
            {
                Console.WriteLine("  SIMD: brak wsparcia sprzętowego lub złe środowisko.");
            }
        }
        // --------------------------------------------------------------------
        // 6. Wykorzystanie CPU (Systemy wielordzeniowe i wieloprocesorowe)
        // --------------------------------------------------------------------
        static void CpuAffinityDemo()
        {
            // Ustawienie affinity dla procesu
            var process = Process.GetCurrentProcess();
            process.ProcessorAffinity = (IntPtr)0x000F; // Użyj pierwszych 4 rdzeni
            Console.WriteLine($"Process Affinity ustawione na: {process.ProcessorAffinity}");

            // Ustawienie affinity dla wątku
            var thread = Thread.CurrentThread;
            thread.Priority = ThreadPriority.Highest;
            Console.WriteLine($"Thread Priority ustawione na: {thread.Priority}");
        }

        // --------------------------------------------------------------------
        // 7. Wykorzystanie CPU (pojedynczy rdzeń)
        // --------------------------------------------------------------------
        static void SingleCoreOptimizationDemo()
        {
            const int N = 10_000_000;

            // SIMD
            if (Settings.UseSimd && Vector.IsHardwareAccelerated)
            {
                var v1 = new Vector<float>(1.0f);
                var v2 = new Vector<float>(2.0f);
                var result = v1 * v2;
                Console.WriteLine($"SIMD wynik: {result[0]}");
            }

            // Inne optymalizacje (do uzupełnienia)
            Console.WriteLine("Dodaj inne optymalizacje dla pojedynczego rdzenia.");
        }
    }
}
