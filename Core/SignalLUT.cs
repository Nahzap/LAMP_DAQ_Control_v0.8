using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LAMP_DAQ_Control_v0._8.Core
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
        
        // Ruta donde se almacenan los archivos LUT
        private static readonly string LutDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LUT");
        
        // Carga una LUT desde un archivo de texto
        public SignalLUT(string fileName)
        {
            // Guardar el nombre del archivo fuente para referencia
            SourceFileName = fileName;
            
            // Verificar que el directorio existe, si no, crearlo
            if (!Directory.Exists(LutDirectory))
            {
                Directory.CreateDirectory(LutDirectory);
                Console.WriteLine($"Se ha creado el directorio LUT en: {LutDirectory}");
            }
            
            string filePath = Path.Combine(LutDirectory, fileName);
            
            // Verificar si el archivo existe
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"El archivo LUT no existe: {filePath}");
            }
            
            Console.WriteLine($"Cargando LUT desde: {filePath}");
            
            // Leer los valores del archivo de texto
            string[] lines = File.ReadAllLines(filePath);
            _size = lines.Length;
            
            // Asignar memoria no administrada para acceso más rápido
            var values = new ushort[_size];
            
            // Cargar los valores del archivo
            for (int i = 0; i < _size; i++)
            {
                if (ushort.TryParse(lines[i], out ushort value))
                {
                    values[i] = value;
                }
                else
                {
                    throw new FormatException($"Formato inválido en el archivo LUT, línea {i + 1}: {lines[i]}");
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
        
        // Método de utilidad para crear un archivo LUT desde valores calculados (solo para inicialización)
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
                Console.WriteLine($"El archivo LUT ya existe: {filePath}");
                return;
            }
            
            Console.WriteLine($"Generando archivo LUT de seno con {size} valores");
            
            // Generar los valores precalculados para un ciclo completo de onda senoidal
            var lines = new List<string>();

            // Para verificar los valores extremos
            double minVal = double.MaxValue;
            double maxVal = double.MinValue;
            double firstQuarterVal = 0;
            double midVal = 0;
            double thirdQuarterVal = 0;
            
            for (int i = 0; i < size; i++)
            {
                // Valor normalizado entre 0 y 2*PI
                double angle = 2.0 * Math.PI * i / size;
                
                // Valor del seno entre -1.0 y 1.0
                double sinValue = Math.Sin(angle);
                
                // Rastrear valores importantes para diagnóstico
                if (sinValue < minVal) minVal = sinValue;
                if (sinValue > maxVal) maxVal = sinValue;
                if (i == size / 4) firstQuarterVal = sinValue;  // 90 grados
                if (i == size / 2) midVal = sinValue;          // 180 grados
                if (i == 3 * size / 4) thirdQuarterVal = sinValue; // 270 grados
                
                // Escalar de [-1, 1] a [0, 65535] con punto medio en 32768
                // El punto cero de la senoidal debe estar exactamente en 32768
                // -1.0 → 0
                // 0.0 → 32768
                // 1.0 → 65535
                ushort scaledValue = (ushort)(Math.Round(32768.0 + (sinValue * 32767.0)));
                lines.Add(scaledValue.ToString());
            }
            
            // Mostrar diagnóstico para verificar que la LUT sea correcta
            Console.WriteLine($"LUT generada: Min={minVal:F4}, Max={maxVal:F4}");
            Console.WriteLine($"Valores de prueba: 90°={firstQuarterVal:F4}, 180°={midVal:F4}, 270°={thirdQuarterVal:F4}");
            
            // Escribir al archivo
            File.WriteAllLines(filePath, lines);
            Console.WriteLine($"Archivo LUT generado: {filePath} con {size} valores");
        }
    }

    // Clase estática con LUTs precargadas desde archivos
    public static class SignalLUTs
    {
        // Nombre del archivo LUT para onda senoidal
        private const string SIN_LUT_FILENAME = "sine_lut.txt";
        
        // Tamaño recomendado para LUTs (64k entradas)
        public const int RECOMMENDED_LUT_SIZE = 65536;
        
        // LUT de onda senoidal estática
        public static readonly SignalLUT SinLUT;
        
        static SignalLUTs()
        {
            try
            {
                // Intentar cargar la LUT desde el archivo
                SinLUT = new SignalLUT(SIN_LUT_FILENAME);
            }
            catch (FileNotFoundException)
            {
                // Si el archivo no existe, generarlo
                Console.WriteLine($"No se encontró el archivo LUT. Generando uno nuevo...");
                SignalLUT.GenerateSineLutFile(SIN_LUT_FILENAME, RECOMMENDED_LUT_SIZE);
                
                // Cargar el archivo recién generado
                SinLUT = new SignalLUT(SIN_LUT_FILENAME);
            }
        }
    }
}
