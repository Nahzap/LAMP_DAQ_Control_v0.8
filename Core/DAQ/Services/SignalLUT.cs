using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    // Clase para el acceso optimizado a tablas de búsqueda cargadas desde archivos
    public unsafe class SignalLUT : IDisposable
    {
        private readonly ushort* _values;
        private readonly int _size;
        private readonly GCHandle _handle;
        private bool _disposed;
        
        public int Size => _size;
        public string SourceFileName { get; }
        
        // Ruta donde se almacenan los archivos LUT (directorio de salida de la aplicación)
        private static readonly string LutDirectory = AppDomain.CurrentDomain.BaseDirectory;
        
        // Nombre completo del archivo LUT para acceso externo
        public static string GetFullLutPath(string fileName)
        {
            // Asegurar que siempre devolvemos la ruta completa al directorio de salida
            return Path.Combine(LutDirectory, fileName);
        }
        
        // Carga una LUT desde un archivo CSV
        public SignalLUT(string fileName)
        {
            // Guardar el nombre del archivo fuente para referencia
            SourceFileName = fileName;
            
            // Verificar que el directorio existe, si no, crearlo
            if (!Directory.Exists(LutDirectory))
            {
                Directory.CreateDirectory(LutDirectory);
                Console.WriteLine($"Se ha creado el directorio para archivos CSV en: {LutDirectory}");
            }
            
            string filePath = Path.Combine(LutDirectory, fileName);
            
            // Verificar si el archivo existe
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"El archivo CSV LUT no existe: {filePath}");
            }
            
            Console.WriteLine($"Cargando LUT desde CSV: {filePath}");
            
            // Leer los valores del archivo CSV
            string[] lines = File.ReadAllLines(filePath);
            
            // Ignorar la primera línea si es un encabezado
            int startIndex = 0;
            if (lines.Length > 0 && lines[0].Contains("Index,Value"))
            {
                startIndex = 1;
            }
            
            _size = lines.Length - startIndex;
            
            // Asignar memoria no administrada para acceso más rápido
            var values = new ushort[_size];
            
            // Cargar los valores del archivo CSV
            for (int i = 0; i < _size; i++)
            {
                string line = lines[i + startIndex];
                string[] parts = line.Split(',');
                
                if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort value))
                {
                    values[i] = value;
                }
                else
                {
                    throw new FormatException($"Formato CSV inválido en el archivo LUT, línea {i + startIndex + 1}: {line}");
                }
            }
            
            // Pinear el array en memoria para acceso rápido
            _handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            _values = (ushort*)_handle.AddrOfPinnedObject().ToPointer();
            
            Console.WriteLine($"LUT cargada con éxito: {_size} valores");
        }

        // Obtener un valor escalado de la LUT basado en una fase normalizada
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetValueRaw(double phase)
        {
            // Normalizar fase a [0, 1.0)
            phase = phase - Math.Floor(phase);
            int index = (int)(phase * _size) % _size;
            return _values[index];
        }
        
        // Obtener un valor normalizado (0.0 - 1.0) de la LUT
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetValueNormalized(double phase)
        {
            return GetValueRaw(phase) / 65535.0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle.IsAllocated)
                {
                    _handle.Free();
                }
                _disposed = true;
            }
        }

        ~SignalLUT()
        {
            Dispose(false);
        }
        
        // Método de utilidad para crear un archivo CSV LUT desde valores calculados (solo para inicialización)
        public static void GenerateSineLutFile(string fileName, int size)
        {
            string filePath = Path.Combine(LutDirectory, fileName);
            
            // Crear el directorio si no existe
            if (!Directory.Exists(LutDirectory))
            {
                Directory.CreateDirectory(LutDirectory);
            }
            
            // Verificar si el archivo ya existe
            if (File.Exists(filePath))
            {
                Console.WriteLine($"El archivo CSV LUT ya existe: {filePath}");
                return;
            }
            
            // Usar exactamente el tamaño solicitado para un periodo completo
            // Esto asegura que tengamos la resolución requerida para una señal limpia
            int lutSize = size;
            Console.WriteLine($"Generando archivo CSV LUT de seno con {lutSize} valores por periodo completo");
            
            // Crear un StringBuilder para construir el CSV de manera eficiente
            var csvBuilder = new StringBuilder();
            
            // Agregar encabezado CSV con más información para optimizar el acceso
            csvBuilder.AppendLine("Index,Value,Phase,Angle,SineValue,TimeOffset");
            
            // Para verificar los valores extremos
            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            
            // Constantes para cálculo de tiempo
            double periodTime = 1.0 / 1000.0; // 1 ms - periodo de referencia (1 kHz)
            
            for (int i = 0; i < lutSize; i++)
            {
                // Fase normalizada [0.0, 1.0) para un periodo completo
                double phase = (double)i / lutSize;
                
                // Ángulo en radianes [0, 2*PI)
                double angle = 2.0 * Math.PI * phase;
                
                // Valor del seno entre -1.0 y 1.0
                double sinValue = Math.Sin(angle);
                
                // Rastrear valores importantes para diagnóstico
                if (sinValue < minVal) minVal = sinValue;
                if (sinValue > maxVal) maxVal = sinValue;
                
                // Escalar de [-1, 1] a [0, 65535] con punto medio en 32768
                // El punto cero de la senoidal debe estar exactamente en 32768
                // -1.0 → 0
                // 0.0 → 32768
                // 1.0 → 65535
                ushort scaledValue = (ushort)(Math.Round(32768.0 + (sinValue * 32767.0)));
                
                // Calcular el offset de tiempo para esta muestra (en microsegundos)
                // Esto ayuda a sincronizar la generación de señal con precisiones de tiempo
                double timeOffsetUs = phase * periodTime * 1000000.0;
                
                // Agregar línea al CSV con toda la información necesaria para minimizar jitter
                csvBuilder.AppendLine($"{i},{scaledValue},{phase:F8},{angle:F8},{sinValue:F8},{timeOffsetUs:F4}");
            }
            
            // Mostrar diagnóstico para verificar que la LUT sea correcta
            Console.WriteLine($"CSV LUT generada: Min={minVal:F4}, Max={maxVal:F4}, Periodo completo");
            Console.WriteLine($"Optimizada para minimizar jitter y mantener pureza de señal");
            
            // Escribir al archivo CSV
            File.WriteAllText(filePath, csvBuilder.ToString());
            Console.WriteLine($"Archivo CSV LUT generado: {filePath} con {lutSize} valores por periodo completo");
        }
    }

    // Clase estática con LUTs precargadas desde archivos
    public static class SignalLUTs
    {
        public static readonly SignalLUT SinLUT;
        public const string SIN_LUT_FILENAME = "sine_lut.csv";
        public const int RECOMMENDED_LUT_SIZE = 65536;
        
        static SignalLUTs()
        {
            // 1. Verificar si existe el archivo CSV
            string filePath = SignalLUT.GetFullLutPath(SIN_LUT_FILENAME);
            bool fileExists = File.Exists(filePath);
            
            // 2. Si no existe, crearlo en formato CSV
            if (!fileExists)
            {
                Console.WriteLine($"No se encontró el archivo LUT CSV. Generando uno nuevo: {filePath}");
                SignalLUT.GenerateSineLutFile(SIN_LUT_FILENAME, RECOMMENDED_LUT_SIZE);
            }
            else
            {
                Console.WriteLine($"Archivo LUT CSV encontrado: {filePath}");
            }
            
            // 3. Comprobar su existencia leyendo el contenido
            try
            {
                // Verificar que el archivo se puede leer correctamente
                string[] lines = File.ReadAllLines(filePath);
                Console.WriteLine($"Archivo CSV LUT verificado: {lines.Length} líneas");
                
                if (lines.Length < 2) // Al menos debe tener cabecera y un valor
                {
                    Console.WriteLine("Archivo CSV LUT inválido o vacío. Regenerando...");
                    SignalLUT.GenerateSineLutFile(SIN_LUT_FILENAME, RECOMMENDED_LUT_SIZE);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al verificar el archivo CSV LUT: {ex.Message}. Regenerando...");
                SignalLUT.GenerateSineLutFile(SIN_LUT_FILENAME, RECOMMENDED_LUT_SIZE);
            }
            
            // 4. Dejar disponible antes de que SignalGenerator la utilice
            try
            {
                // Cargar la LUT desde el archivo (solo para verificar que está disponible)
                SinLUT = new SignalLUT(SIN_LUT_FILENAME);
                Console.WriteLine($"Archivo CSV LUT disponible para SignalGenerator: {SIN_LUT_FILENAME} con {SinLUT.Size} valores");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fatal al cargar la LUT: {ex.Message}");
                throw; // Este error es crítico y debe detener la ejecución
            }
        }
    }
}
