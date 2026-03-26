using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LAMP_DAQ_Control_v0_8.Core.DAQ;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;
using LAMP_DAQ_Control_v0_8.UI;

namespace LAMP_DAQ_Control_v0_8
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        [STAThread] // Requerido para WPF
        static void Main(string[] args)
        {
            // Detectar modo: -console para modo consola, default es WPF
            bool useConsole = args.Length > 0 && args[0].ToLower() == "-console";
            
            if (useConsole)
            {
                RunConsoleMode().Wait();
            }
            else
            {
                RunWPFMode();
            }
        }
        
        static async Task RunConsoleMode()
        {
            Console.Title = "Controlador DAQ Multidevice";
            
            try
            {
                Console.WriteLine("=== Sistema de Control DAQ Multidevice ===");
                Console.WriteLine("Detectando dispositivos disponibles...");
                
                // Crear el controlador DAQ
                using (var controller = new DAQController())
                {
                    // Crear la interfaz de usuario con el controlador
                    var ui = new ConsoleUI(controller);
                    
                    // Ejecutar la interfaz de usuario
                    await ui.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR FATAL: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"DETALLES: {ex.InnerException.Message}");
                }
            }
            finally
            {
                Console.WriteLine("\nPresione cualquier tecla para salir...");
                Console.ReadKey();
            }
        }
        
        static void RunWPFMode()
        {
            // Asignar ventana de consola para mostrar logs
            AllocConsole();

            // Configurar interceptor de logs global
            string logDir = @"c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\logs";
            string logFile = Path.Combine(logDir, "LAMP_DAQ_Session.log");
            var logInterceptor = new TimestampedLogWriter(Console.Out, logFile);
            Console.SetOut(logInterceptor);
            Console.SetError(logInterceptor);

            Console.Title = "LAMP DAQ Control v0.8 - Sistema de Logs";
            Console.WriteLine("========================================");
            Console.WriteLine("LAMP DAQ Control v0.8 - Sistema de Logs");
            Console.WriteLine("========================================");
            Console.WriteLine("Esta ventana muestra todos los logs del sistema en tiempo real.");
            Console.WriteLine("NO CERRAR esta ventana - se cerrará automáticamente al salir de la aplicación.");
            Console.WriteLine("========================================");
            Console.WriteLine();
            
            var app = new UI.WPF.App();
            app.InitializeComponent();
            app.Run();
        }
    }

    /// <summary>
    /// Intercepta las llamadas a la Consola para agregar un Timestamp universal y 
    /// escribirlas simultáneamente en un archivo de texto.
    /// </summary>
    public class TimestampedLogWriter : TextWriter
    {
        private readonly TextWriter _originalOut;
        private readonly StreamWriter _fileWriter;
        private bool _isNewLine = true;

        public TimestampedLogWriter(TextWriter originalOut, string logFilePath)
        {
            _originalOut = originalOut;

            string dir = Path.GetDirectoryName(logFilePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Create destruye/sobreescribe el archivo cada vez que inicia la app
            _fileWriter = new StreamWriter(new FileStream(logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read));
            _fileWriter.AutoFlush = true;
        }

        public override Encoding Encoding => _originalOut.Encoding;

        private void WriteTimestampIfNeeded()
        {
            if (_isNewLine)
            {
                string timestamp = $"[{DateTime.Now:dd-MM-yyyy}] [{DateTime.Now:HH:mm:ss}] ";
                _originalOut.Write(timestamp);
                _fileWriter.Write(timestamp);
                _isNewLine = false;
            }
        }

        public override void Write(char value)
        {
            if (value != '\r' && value != '\n')
            {
                WriteTimestampIfNeeded();
            }

            _originalOut.Write(value);
            _fileWriter.Write(value);

            if (value == '\n')
            {
                _isNewLine = true;
            }
        }

        public override void Write(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            foreach (char c in value)
            {
                Write(c);
            }
        }
        
        public override void Write(char[] buffer, int index, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Write(buffer[index + i]);
            }
        }

        public override void WriteLine()
        {
            _originalOut.WriteLine();
            _fileWriter.WriteLine();
            _isNewLine = true;
        }

        public override void WriteLine(string value)
        {
            Write(value);
            WriteLine();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fileWriter?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
