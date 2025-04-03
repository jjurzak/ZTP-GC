using System.Buffers;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace GC_dotMemory
{
    // Klasa reprezentująca bitmapę w unmanaged memory z implementacją IDisposable
    public class UnmanagedBitmap : IDisposable
    {
        public IntPtr Buffer { get; private set; }
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        private bool _disposedValue;
 
        public UnmanagedBitmap(int width, int height)
        {
            Width = width;
            Height = height;
            Stride = width;
            Buffer = Marshal.AllocHGlobal(width * height);
        }
 
        public void Fill(byte value)
        {
            unsafe
            {
                byte* ptr = (byte*)Buffer;
                for (int i = 0; i < Width * Height; i++)
                {
                    ptr[i] = value;
                }
            }
        }
 
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (Buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Buffer);
                    Buffer = IntPtr.Zero;
                }
                _disposedValue = true;
            }
        }
 
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
 
        ~UnmanagedBitmap()
        {
            Dispose(disposing: false);
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
                Console.WriteLine("2. Operacja na bitmapie w unmanaged memory");
                Console.WriteLine("3. Operacja na bitmapie w managed memory (byte[,])");
                Console.WriteLine("4. Analiza wykorzystania pamięci (symulacja, sprawdź narzędzia zewnętrzne)");
                Console.WriteLine("5. Zmiana parametrów GC i wymuszenie GC.Collect (symulacja konfiguracji GC)");
                Console.WriteLine("6. Techniki optymalizacji kodu (pooling, fixed, Dispose, LowLatencyMode)");
                Console.WriteLine("0. Wyjście");
                Console.Write("Wybierz opcję (0-6): ");
 
                string choice = Console.ReadLine();
 
                switch (choice)
                {
                    case "1":
                        ExecuteAction("Mnożenie macierzy", MatrixMultiplicationDemo);
                        break;
                    case "2":
                        ExecuteAction("Bitmapa w unmanaged memory", UnmanagedBitmapDemo);
                        break;
                    case "3":
                        ExecuteAction("Bitmapa w managed memory", ManagedBitmapDemo);
                        break;
                    case "4":
                        ExecuteAction("GC: wymuszenie zbierania i konfiguracja parametrów", GCDemo);
                        break;
                    case "5":
                        ExecuteAction("Techniki optymalizacji", OptimizationDemo);
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
 
        // Metoda pomocnicza do wykonywania akcji z pomiarem czasu
        static void ExecuteAction(string actionName, Action action)
        {
            Console.WriteLine($"\n--- {actionName} ---");
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                action.Invoke();
            }
            sw.Stop();
            Console.WriteLine($"{actionName} zakończone w: {sw.ElapsedMilliseconds} ms");
        }
 
        // 1. Mnożenie macierzy – duża macierz (3000x200) * mała macierz (200x5)
        static void MatrixMultiplicationDemo()
        {
            int rowsA = 30000, colsA = 200, colsB = 5;
            double[,] largeMatrix = new double[rowsA, colsA];
            double[,] smallMatrix = new double[colsA, colsB];
            double[,] result = new double[rowsA, colsB];
 
            Random rnd = new Random();
            for (int i = 0; i < rowsA; i++)
                for (int j = 0; j < colsA; j++)
                    largeMatrix[i, j] = rnd.NextDouble();
 
            for (int i = 0; i < colsA; i++)
                for (int j = 0; j < colsB; j++)
                    smallMatrix[i, j] = rnd.NextDouble();
 
            // Mnożenie macierzy
            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < colsA; k++)
                    {
                        sum += largeMatrix[i, k] * smallMatrix[k, j];
                    }
                    result[i, j] = sum;
                }
            }
            Console.WriteLine("Mnożenie macierzy wykonane.");
        }
 
        // 2. Operacja na bitmapie w unmanaged memory
        static void UnmanagedBitmapDemo()
        {
            int width = 1024, height = 768;
            using (var bitmap = new UnmanagedBitmap(width, height))
            {
                bitmap.Fill(128);
                Console.WriteLine("Unmanaged bitmapa wypełniona wartością 128.");
                // Przykład operacji na pikselach za pomocą unsafe
                unsafe
                {
                    byte* ptr = (byte*)bitmap.Buffer;
                    if (ptr != null)
                    {
                        ptr[0] = 255; 
                    }
                }
            }
            Console.WriteLine("Zwrot zasobów unmanaged przez Dispose().");
        }
 
        // 3. Operacja na bitmapie w managed memory
        static void ManagedBitmapDemo()
        {
            int width = 1024, height = 768;
            byte[,] bitmap = new byte[height, width];
 
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    bitmap[i, j] = 128;
 
            bitmap[0, 0] = 255;
            Console.WriteLine("Managed bitmapa (byte[,]) przetworzona.");
        }
 
        // 4. Symulacja analizy pamięci – rzeczywista analiza wykonywana przez narzędzia zewnętrzne takich jak DotMemory lub PerfView

 
        // 5. Wymuszenie GC.Collect i zmiana ustawień GC (przykład: LowLatency, kompaktacja LOH itp.)
        static void GCDemo()
        {
            Console.WriteLine("GC demo - symulacja konfiguracji GC i wymuszenia zbierania śmieci.");
 
            long memBefore = GC.GetTotalMemory(false);
            Console.WriteLine($"Pamięć przed alokacją: {memBefore} bajtów.");
 
            for (int i = 0; i < 10000; i++)
            {
                var temp = new byte[1024];
            }
            
            GC.Collect(0);
            GC.WaitForPendingFinalizers();
 
            // Przykładowa kompaktacja Large Object Heap
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
 
            long memAfter = GC.GetTotalMemory(true);
            Console.WriteLine($"Pamięć po GC: {memAfter} bajtów.");
 
            Console.WriteLine("Dodatkowe ustawienia GC (workstation vs. server, HeapHardLimit, HighMemoryPercent etc.) ustawia się przy pomocy runtimeconfig.json lub zmiennych środowiskowych.");
        }
 
        // 6. Techniki optymalizacji kodu – pooling, fixed, Dispose, zmiana trybu GC (LowLatencyMode)
        static void OptimizationDemo()
        {
            Console.WriteLine("Techniki optymalizacji kodu:");
 
            // a) Pooling - wykorzystanie ArrayPool
            var pool = ArrayPool<byte>.Shared;
            byte[] buffer = pool.Rent(1024 * 1024);
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)(i % 256);
            pool.Return(buffer);
            Console.WriteLine("Pooling – bufor został wypożyczony i zwrócony.");
 
            // b) Użycie fixed do pinowania pamięci
            byte[] managedArray = new byte[1024];
            unsafe
            {
                fixed (byte* ptr = managedArray)
                {
                    for (int i = 0; i < managedArray.Length; i++)
                        ptr[i] = 42;
                }
            }
            Console.WriteLine("Fixed – pamięć została przypięta podczas operacji.");
 
            // c) Wzorzec Dispose – użyty przy UnmanagedBitmap
            // d) Przykład ustawienia trybu niskiej latencji GC
            var previousLatency = GCSettings.LatencyMode;
            try
            {
                GCSettings.LatencyMode = GCLatencyMode.LowLatency;
                Thread.Sleep(100); // symulacja operacji krytycznej
            }
            finally
            {
                GCSettings.LatencyMode = previousLatency;
            }
            Console.WriteLine("GC ustawiony w tryb LowLatency dla operacji krytycznych.");
 
            // e) Rejestracja powiadomień GC – tego typu mechanizmy warto eksplorować, gdyż realna implementacja zależy od konkretnej potrzeby.
            Console.WriteLine("Rejestracja powiadomień GC – funkcjonalność do rozszerzenia (sprawdź dokumentację GC).");
        }
    }
}