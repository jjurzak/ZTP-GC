using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime;
using System.Runtime.InteropServices;

// Ten using może być niepotrzebny, jeśli nie używasz IValueTaskSource

namespace GC_dotMemory
{
    // ------------------------------------------------------------------------
    // 1. SETTINGS - tryby sterowane zmiennymi środowiskowymi
    // ------------------------------------------------------------------------
    static class Settings
    {
        public static int CustomCollect =>
            int.TryParse(Environment.GetEnvironmentVariable("ztp_custom_collect"), out var val) ? val : -1;

        public static bool UsePooling =>
            true;
        public static int UnmanagedMode =>
            1;
        public static bool UseDispose =>
            true;

        public static int? ParallelLoopDegree =>
            1;

        public static bool UseTasks =>
            true;

        public static bool UseSimd =>
            true;
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
            // Console.WriteLine($"UnmanagedBitmap allocated: {width*height} bytes at {Buffer.ToInt64():X}");
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
                    // Console.WriteLine($"UnmanagedBitmap freeing: {Width*Height} bytes at {Buffer.ToInt64():X}");
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

        ~UnmanagedBitmap()
        {
            // Console.WriteLine($"UnmanagedBitmap finalizing: {Width*Height} bytes at {Buffer.ToInt64():X}");
            Dispose(false);
        }
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
                Console.WriteLine("6. Wykorzystanie CPU (Systemy wielordzeniowe i wieloprocesorowe)");
                Console.WriteLine("7. Wykorzystanie CPU (pojedynczy rdzeń)");
                Console.WriteLine("0. Wyjście");
                Console.Write("Wybierz opcję (0-7): "); // Zaktualizowano zakres opcji
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

        // wrapper z pomiarem czasu i wielokrotnym wykonaniem
        static void ExecuteAction(string name, Action action)
        {
            Console.WriteLine($"\n--- Rozpoczęcie serii dla: {name} ---");
            var totalSw = Stopwatch.StartNew();
            const int repetitions = 5;
            const int delayMilliseconds = 2000;

            for (int i = 0; i < repetitions; i++)
            {
                Console.WriteLine($"\n  Uruchomienie {i + 1}/{repetitions} dla: {name}");
                var iterationSw = Stopwatch.StartNew();
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    BŁĄD podczas wykonania {name}: {ex.Message}");
                }
                iterationSw.Stop();
                Console.WriteLine($"  {name} (uruchomienie {i + 1}) zakończone w: {iterationSw.ElapsedMilliseconds} ms");

                if (i < repetitions - 1)
                {
                    Console.WriteLine($"  Pauza {delayMilliseconds / 1000} sekundy...");
                    Thread.Sleep(delayMilliseconds);
                }
            }
            totalSw.Stop();
            Console.WriteLine($"\n--- Seria dla: {name} ({repetitions} uruchomień) zakończona. Całkowity czas: {totalSw.ElapsedMilliseconds} ms (wliczając pauzy) ---");
        }

        // --------------------------------------------------------------------
        // 1. Mnożenie macierzy 30000×200 * 200×5
        // --------------------------------------------------------------------
        static void MatrixMultiplicationDemo()
        {
            int rowsA = 30000, colsA = 200, colsB = 5;
            var A = new double[rowsA, colsA];
            var B = new double[colsA, colsB];
            var C = new double[rowsA, colsB]; // Wynik mnożenia
            var rnd = new Random(42); // Użycie stałego ziarna dla powtarzalności (opcjonalne)

            // Inicjalizacja macierzy A
            for (int i = 0; i < rowsA; i++)
                for (int j = 0; j < colsA; j++)
                    A[i, j] = rnd.NextDouble();

            // Inicjalizacja macierzy B
            for (int i = 0; i < colsA; i++)
                for (int j = 0; j < colsB; j++)
                    B[i, j] = rnd.NextDouble();

            // Mnożenie macierzy
            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < colsA; k++) // colsA jest równe rowsB
                    {
                        sum += A[i, k] * B[k, j];
                    }
                    C[i, j] = sum;
                }
            }
            // C[0,0] = 0; // Celowe "użycie" C, aby uniknąć potencjalnej optymalizacji przez kompilator
            Console.WriteLine($"  Mnożenie macierzy [{rowsA}x{colsA}] * [{colsA}x{colsB}] wykonane. Wynik C[0,0]={C[0,0]:F2}");
        }

        // --------------------------------------------------------------------
        // 2. Przetwarzanie bitmapy w różnych trybach
        // --------------------------------------------------------------------
        static void ProcessBitmapDemo()
        {
            const int width = 512, height = 512;
            const int iterations = 10; // Wykonaj operację na bitmapie kilka razy, aby była bardziej znacząca

            Console.WriteLine($"  Przetwarzanie bitmapy {width}x{height} (iteracje: {iterations})");

            for(int i=0; i<iterations; i++)
            {
                // wybór trybu managed vs unmanaged
                if (Settings.UnmanagedMode == 1)
                {
                    if (Settings.UseDispose)
                    {
                        using var bm = new UnmanagedBitmap(width, height);
                        bm.Fill(128);
                    }
                    else
                    {
                        var bm = new UnmanagedBitmap(width, height); // Potencjalny wyciek, jeśli nie ma GC
                        bm.Fill(128);
                        // GC.SuppressFinalize(bm); // jeśli nie chcemy finalizatora w tym scenariuszu
                    }
                    // Komunikat po pętli, aby nie spamować
                }
                else if (Settings.UnmanagedMode == 2)
                {
                    // Inny wariant - tutaj symulujemy go tak samo jak mode 1
                    // W rzeczywistym scenariuszu mógłby to być np. AllocCoTaskMem
                    if (Settings.UseDispose)
                    {
                        using var bm = new UnmanagedBitmap(width, height); // Używamy tej samej klasy dla uproszczenia
                        bm.Fill(64);
                    }
                    else
                    {
                        var bm = new UnmanagedBitmap(width, height);
                        bm.Fill(64);
                    }
                     // Komunikat po pętli
                }
                else // Managed mode (UnmanagedMode == 0)
                {
                    if (Settings.UsePooling)
                    {
                        var temp = ArrayPool<byte>.Shared.Rent(width * height);
                        try
                        {
                            for (int px = 0; px < temp.Length; px++) temp[px] = 128;
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(temp);
                        }
                    }
                    else
                    {
                        var managed = new byte[height, width];
                        for (int y = 0; y < height; y++)
                            for (int x = 0; x < width; x++)
                                managed[y, x] = 128;
                    }
                     // Komunikat po pętli
                }
            }

            // Informacja o zakończeniu operacji na bitmapie
            if (Settings.UnmanagedMode == 1) Console.WriteLine($"    UnmanagedBitmap (mode=1, dispose={Settings.UseDispose}) wypełnione {iterations}x.");
            else if (Settings.UnmanagedMode == 2) Console.WriteLine($"    UnmanagedBitmap (mode=2 symulowany, dispose={Settings.UseDispose}) wypełnione {iterations}x.");
            else if (Settings.UsePooling) Console.WriteLine($"    Managed bitmap + pooling użyte do symulacji fill() {iterations}x.");
            else Console.WriteLine($"    Managed bitmap (byte[,]) wypełniona {iterations}x.");


            // ewentualne wymuszenie GC
            if (Settings.CustomCollect >= 0)
            {
                Console.WriteLine($"    Wymuszanie GC.Collect({Settings.CustomCollect})...");
                GC.Collect(Settings.CustomCollect, GCCollectionMode.Forced, true); // Użyj blocking: true
                GC.WaitForPendingFinalizers();
                Console.WriteLine($"    Wymuszono GC.Collect({Settings.CustomCollect}).");
            }
        }

        // --------------------------------------------------------------------
        // 3. Symulacja analizy pamięci (instrukcja)
        // --------------------------------------------------------------------
        static void MemoryAnalysisDemo()
        {
            Console.WriteLine("  Instrukcja: Aby przeprowadzić analizę pamięci, użyj zewnętrznych narzędzi profilujących.");
            Console.WriteLine("  Przykłady narzędzi: DotMemory, PerfView (dla Windows), Visual Studio Diagnostic Tools.");
            Console.WriteLine("  Uruchom program z odpowiednimi zmiennymi środowiskowymi (np. ztp_pooling=1),");
            Console.WriteLine("  a następnie podłącz profiler do procesu lub uruchom program pod kontrolą profilera.");
            Console.WriteLine("  Obserwuj alokacje, użycie pamięci, czas życia obiektów i wpływ GC.");
            Console.WriteLine("  Spróbuj różnych kombinacji ustawień (pooling, unmanaged, dispose), aby zobaczyć różnice.");
        }

        // --------------------------------------------------------------------
        // 4. GC demo: LatencyMode, LOH compact, GC.Collect
        // --------------------------------------------------------------------
        static void GCDemo()
        {
            Console.WriteLine("  GC Demo:");
            GCLatencyMode originalLatencyMode = GCSettings.LatencyMode;
            Console.WriteLine($"    LatencyMode przed: {originalLatencyMode}");

            try
            {
                GCSettings.LatencyMode = GCLatencyMode.LowLatency;
                Console.WriteLine($"    LatencyMode po ustawieniu LowLatency: {GCSettings.LatencyMode}");
                // Symulacja operacji wrażliwej na opóźnienia
                Thread.Sleep(100); // Krótka operacja
            }
            finally
            {
                GCSettings.LatencyMode = originalLatencyMode; // Przywrócenie oryginalnego trybu
                Console.WriteLine($"    LatencyMode przywrócony do: {GCSettings.LatencyMode}");
            }


            long before = GC.GetTotalMemory(false);
            Console.WriteLine($"    Pamięć przed alokacjami LOH: {before / 1024:N0} KB");

            // alokacje LOH
            const int numLohObjects = 20; // Mniejsza liczba, aby nie wyczerpać pamięci szybko przy wielokrotnych uruchomieniach
            var list = new byte[numLohObjects][];
            for (int i = 0; i < list.Length; i++)
            {
                list[i] = new byte[85_000 + i]; // Obiekty na LOH, zróżnicowany rozmiar
            }
            Console.WriteLine($"    Zaalokowano {numLohObjects} obiektów na LOH.");
            long afterAlloc = GC.GetTotalMemory(false);
            Console.WriteLine($"    Pamięć po alokacjach LOH (przed GC): {afterAlloc / 1024:N0} KB");


            // kompaktacja LOH
            GCLargeObjectHeapCompactionMode originalLohCompactionMode = GCSettings.LargeObjectHeapCompactionMode;
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            Console.WriteLine("    Ustawiono LOHCompactionMode na CompactOnce.");

            Console.WriteLine("    Wymuszanie GC.Collect(2, Forced, blocking: true, compacting: true)...");
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers(); // Upewnij się, że finalizatory zostały uruchomione
            Console.WriteLine("    Zakończono GC.Collect z kompaktacją.");

            GCSettings.LargeObjectHeapCompactionMode = originalLohCompactionMode; // Przywróć
            long afterGC = GC.GetTotalMemory(true); // true - wymuś kolekcję, aby uzyskać dokładniejszy pomiar
            Console.WriteLine($"    Pamięć po GC i kompaktacji LOH: {afterGC / 1024:N0} KB");
            Console.WriteLine($"    Zredukowano pamięć o: {(afterAlloc - afterGC) / 1024:N0} KB");

            // Upewnij się, że lista jest nadal dostępna, aby uniknąć przedwczesnego zwolnienia przez GC
            // (chociaż GC.Collect było blokujące, to dobra praktyka)
            if (list[0] != null && list[0].Length > 0)
            {
                 // Console.WriteLine("    Obiekty LOH nadal w użyciu.");
            }
            list = null; // Pozwól GC zwolnić obiekty LOH w następnym cyklu
        }

        // --------------------------------------------------------------------
        // 5. Techniki optymalizacji: pooling, fixed, parallel, tasks, SIMD
        // --------------------------------------------------------------------
        static void OptimizationDemo()
        {
            const int N = 10_000_000; // Rozmiar danych dla niektórych testów
            Console.WriteLine("  Techniki optymalizacji:");

            // a) Pooling
            if (Settings.UsePooling)
            {
                var pool = ArrayPool<byte>.Shared;
                var buf = pool.Rent(N);
                try
                {
                    for (int i = 0; i < N; i++) buf[i] = (byte)(i % 256);
                    Console.WriteLine($"    Pooling: bufor {N} bajtów rented/returned.");
                }
                finally
                {
                    pool.Return(buf);
                }
            }
            else
            {
                var buf = new byte[N];
                for (int i = 0; i < N; i++) buf[i] = (byte)(i % 256);
                Console.WriteLine($"    Standardowa alokacja: bufor {N} bajtów utworzony i wypełniony (będzie podlegał GC).");
            }

            // b) fixed
            {
                var arr = new byte[1024];
                unsafe
                {
                    fixed (byte* p = arr)
                    {
                        for (int i = 0; i < arr.Length; i++) p[i] = 1;
                    }
                }
                Console.WriteLine("    fixed: przypięto tablicę byte[1024] w pamięci i wypełniono ją.");
            }

            // c) LowLatencyMode (SustainedLowLatency)
            var prevLatencyMode = GCSettings.LatencyMode;
            try
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
                Console.WriteLine($"    LowLatencyMode: Ustawiono SustainedLowLatency (poprzedni: {prevLatencyMode}).");
                // Symulacja operacji, która korzysta z tego trybu
                byte[] tempArray = new byte[1024 * 10]; // Mała alokacja, która nie powinna wywołać pełnego GC
                Thread.Sleep(50); // Symulacja pracy
            }
            finally
            {
                GCSettings.LatencyMode = prevLatencyMode;
                Console.WriteLine($"    LowLatencyMode: Przywrócono {prevLatencyMode}.");
            }


            // d) parallel loop
            if (Settings.ParallelLoopDegree.HasValue && Settings.ParallelLoopDegree.Value > 0)
            {
                long sum = 0; // Zmienna do zapisu wyniku, aby uniknąć optymalizacji
                Parallel.For(0, N / 1000, new ParallelOptions { MaxDegreeOfParallelism = Settings.ParallelLoopDegree.Value }, i =>
                {
                    // dummy work
                    long x = (long)i * i;
                    Interlocked.Add(ref sum, x % 100); // Prosta operacja na sumie
                });
                Console.WriteLine($"    Parallel.For z MaxDegreeOfParallelism={Settings.ParallelLoopDegree.Value} wykonane (suma kontrolna: {sum}).");
            }
            else
            {
                Console.WriteLine("    Parallel.For pominięty (ParallelLoopDegree nieustawione lub <= 0).");
            }

            // e) Tasks
            if (Settings.UseTasks)
            {
                var tasks = new Task<long>[4];
                for (int t = 0; t < tasks.Length; t++)
                {
                    int taskNum = t; // Przechwycenie zmiennej pętli
                    tasks[t] = Task.Run(() =>
                    {
                        long localSum = 0;
                        for (int i = 0; i < N / (4 * 1000); i++) // Podziel pracę
                        {
                            localSum += (long)i * i % 100;
                        }
                        return localSum;
                    });
                }
                Task.WhenAll(tasks).Wait(); // Czekaj na wszystkie
                long totalSum = 0;
                foreach(var t in tasks) totalSum += t.Result;
                Console.WriteLine($"    Tasks: zakończono {tasks.Length} zadań równoległych (suma kontrolna: {totalSum}).");
            }
            else
            {
                Console.WriteLine("    Tasks pominięte (UseTasks nieustawione).");
            }

            // f) SIMD
            if (Settings.UseSimd)
            {
                if (Vector.IsHardwareAccelerated)
                {
                    int vectorSize = Vector<float>.Count;
                    int arraySize = vectorSize * 1000; // Upewnij się, że rozmiar jest wielokrotnością vectorSize
                    float[] arr1 = new float[arraySize];
                    float[] arr2 = new float[arraySize];
                    float[] resultArr = new float[arraySize];

                    for (int i = 0; i < arraySize; i++)
                    {
                        arr1[i] = i + 0.5f;
                        arr2[i] = i + 1.5f;
                    }

                    for (int i = 0; i < arraySize; i += vectorSize)
                    {
                        var v1 = new Vector<float>(arr1, i);
                        var v2 = new Vector<float>(arr2, i);
                        var sum = v1 + v2; // Operacja SIMD
                        sum.CopyTo(resultArr, i);
                    }
                    Console.WriteLine($"    SIMD: Vector<float>.Count = {vectorSize}. Dodano dwie tablice float[{arraySize}]. Wynik[0]={resultArr[0]:F1}. Hardware accelerated: Tak.");
                }
                else
                {
                    Console.WriteLine("    SIMD: Włączone, ale Vector.IsHardwareAccelerated == false. Brak wsparcia sprzętowego lub nie jest aktywne.");
                }
            }
            else
            {
                Console.WriteLine("    SIMD pominięte (UseSimd nieustawione).");
            }
        }
        // --------------------------------------------------------------------
        // 6. Wykorzystanie CPU (Systemy wielordzeniowe i wieloprocesorowe)
        // --------------------------------------------------------------------
        static void CpuAffinityDemo()
        {
            Console.WriteLine("  Demonstracja ustawień CPU (Affinity, Priority):");
            try
            {
                var process = Process.GetCurrentProcess();
                IntPtr originalAffinity = process.ProcessorAffinity;
                Console.WriteLine($"    Bieżące Process Affinity: 0x{originalAffinity.ToInt64():X} (Rdzenie: {Convert.ToString(originalAffinity.ToInt64(), 2).PadLeft(Environment.ProcessorCount, '0')})");

                // Ustawienie affinity na pierwsze 2 rdzenie (jeśli dostępne)
                long newAffinityMask = 0x03; // Bity dla rdzenia 0 i 1
                if (Environment.ProcessorCount == 1) newAffinityMask = 0x01; // Jeśli tylko 1 rdzeń, użyj go

                process.ProcessorAffinity = (IntPtr)newAffinityMask;
                Console.WriteLine($"    Process Affinity ustawione na: 0x{process.ProcessorAffinity.ToInt64():X} (Rdzenie: {Convert.ToString(process.ProcessorAffinity.ToInt64(), 2).PadLeft(Environment.ProcessorCount, '0')})");
                
                // Symulacja pracy
                Thread.Sleep(100);

                // Przywrócenie oryginalnego affinity
                process.ProcessorAffinity = originalAffinity;
                Console.WriteLine($"    Process Affinity przywrócone do: 0x{process.ProcessorAffinity.ToInt64():X}");

                var thread = Thread.CurrentThread;
                ThreadPriority originalPriority = thread.Priority;
                Console.WriteLine($"    Bieżący Thread Priority: {originalPriority}");

                thread.Priority = ThreadPriority.Highest;
                Console.WriteLine($"    Thread Priority ustawione na: {thread.Priority}");
                
                // Symulacja pracy
                Thread.Sleep(100);

                thread.Priority = originalPriority;
                Console.WriteLine($"    Thread Priority przywrócone do: {thread.Priority}");
            }
            catch (PlatformNotSupportedException)
            {
                Console.WriteLine("    Ustawienie ProcessorAffinity lub ThreadPriority nie jest wspierane na tej platformie.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Wystąpił błąd podczas ustawiania affinity/priority: {ex.Message}");
            }
        }

        // --------------------------------------------------------------------
        // 7. Wykorzystanie CPU (pojedynczy rdzeń)
        // --------------------------------------------------------------------
        static void SingleCoreOptimizationDemo()
        {
            Console.WriteLine("  Demonstracja optymalizacji dla pojedynczego rdzenia:");
            const int N_SINGLE_CORE = 5_000_000; // Mniejszy rozmiar dla szybszego wykonania

            // Symulacja operacji, która może być zoptymalizowana
            double sum = 0;
            var data = new float[N_SINGLE_CORE];
            for(int i=0; i<data.Length; ++i) data[i] = (float)i / (i+1.0f);


            // Wersja standardowa
            var swStandard = Stopwatch.StartNew();
            for (int i = 0; i < N_SINGLE_CORE; i++)
            {
                sum += Math.Sin(data[i]) * Math.Cos(data[i]); // Przykładowa praca
            }
            swStandard.Stop();
            Console.WriteLine($"    Standardowa pętla: suma={sum:F2}, czas={swStandard.ElapsedMilliseconds} ms");

            sum = 0; // Reset sumy

            // Wersja zoptymalizowana (np. unikanie powtarzalnych obliczeń, lepsze wykorzystanie cache - tutaj trudne do pokazania w prosty sposób bez SIMD)
            // W tym przykładzie, jeśli SIMD jest dostępne, użyjemy go.
            // W przeciwnym razie, różnica może być niewielka lub żadna.

            if (Settings.UseSimd && Vector.IsHardwareAccelerated)
            {
                sum = 0;
                var swSimd = Stopwatch.StartNew();
                int vectorSize = Vector<float>.Count;
                var sinValues = new Vector<float>();
                var cosValues = new Vector<float>();
                var partialSums = Vector<float>.Zero;

                // Wypełnienie tablicy `data` wcześniej

                for (int i = 0; i <= N_SINGLE_CORE - vectorSize; i += vectorSize)
                {
                    var vData = new Vector<float>(data, i);
                    // Math.Sin i Math.Cos nie mają bezpośrednich odpowiedników SIMD w .NET Core < 7.0
                    // Dla .NET 7+ można użyć VectorMath.Sin, VectorMath.Cos (z System.Runtime.Intrinsics.X86)
                    // Tutaj symulujemy przez prostą operację, np. mnożenie
                    // Aby to było bardziej realistyczne, potrzebowalibyśmy implementacji funkcji trygonometrycznych dla wektorów
                    // lub użylibyśmy operacji, które są łatwo wektoryzowalne, np. A[i] * B[i] + C[i]
                    var tempProduct = vData * vData; // Zamiast sin*cos, użyjmy czegoś co działa z Vector<T>
                    partialSums += tempProduct;
                }
                // Zsumuj elementy wektora partialSums
                for(int k=0; k < Vector<float>.Count; ++k) sum += partialSums[k];

                // Przetwarzanie pozostałych elementów (jeśli N_SINGLE_CORE nie jest wielokrotnością vectorSize)
                for (int i = (N_SINGLE_CORE / vectorSize) * vectorSize; i < N_SINGLE_CORE; i++)
                {
                     sum += data[i] * data[i]; // Ta sama operacja co w pętli SIMD
                }
                swSimd.Stop();
                Console.WriteLine($"    Pętla z użyciem SIMD (symulacja): suma={sum:F2}, czas={swSimd.ElapsedMilliseconds} ms (Vector<float>.Count={vectorSize})");
            }
            else
            {
                Console.WriteLine("    SIMD nieaktywne lub niewspierane dla tego testu.");
            }
            Console.WriteLine("  Uwaga: Proste pętle są często optymalizowane przez kompilator JIT. Rzeczywiste korzyści z ręcznych optymalizacji są widoczne w bardziej złożonych scenariuszach.");
        }
    }
}