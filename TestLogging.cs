using System;
using System.Threading;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Services;

namespace LAMP_DAQ_Control_v0_8
{
    /// <summary>
    /// Programa de prueba simple para verificar el sistema de logging
    /// </summary>
    class TestLogging
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== PRUEBA DEL SISTEMA DE LOGGING ===\n");
            
            // 1. Probar FileLogger
            Console.WriteLine("1. Creando FileLogger...");
            var fileLogger = new FileLogger();
            Console.WriteLine($"   Log file: {fileLogger.GetCurrentLogFilePath()}\n");
            
            // 2. Probar ConsoleLogger
            Console.WriteLine("2. Creando ConsoleLogger...");
            var consoleLogger = new ConsoleLogger();
            
            // 3. Probar CompositeLogger
            Console.WriteLine("3. Creando CompositeLogger (File + Console)...\n");
            var compositeLogger = new CompositeLogger(fileLogger, consoleLogger);
            
            // 4. Probar ActionLogger
            Console.WriteLine("4. Creando ActionLogger...\n");
            var actionLogger = new ActionLogger(compositeLogger);
            
            // 5. Probar diferentes tipos de logs
            Console.WriteLine("=== PROBANDO DIFERENTES TIPOS DE LOGS ===\n");
            
            actionLogger.LogUserAction("TestLogging Started", "Testing logging system");
            Thread.Sleep(100);
            
            actionLogger.LogButtonClick("TestButton", "TestViewModel");
            Thread.Sleep(100);
            
            actionLogger.LogValueChange("TestProperty", 0, 100, "TestSource");
            Thread.Sleep(100);
            
            actionLogger.LogAnalogWrite(0, 5.0, true);
            Thread.Sleep(100);
            
            actionLogger.LogDigitalWrite(0, null, 0xFF, true);
            Thread.Sleep(100);
            
            actionLogger.LogSignalStart(0, 100.0, 5.0, 5.0);
            Thread.Sleep(100);
            
            actionLogger.LogRampStart(0, 0.0, 10.0, 1000);
            Thread.Sleep(100);
            
            actionLogger.LogDeviceInitialization("PCIe-1824", 0, true);
            Thread.Sleep(100);
            
            actionLogger.StartTiming();
            Thread.Sleep(50);
            actionLogger.StopTiming("Test Operation");
            
            // 6. Probar logging con excepción
            Console.WriteLine("\n=== PROBANDO LOG DE EXCEPCIÓN ===\n");
            try
            {
                throw new InvalidOperationException("Esta es una excepción de prueba");
            }
            catch (Exception ex)
            {
                actionLogger.LogException("TestLogging", ex);
            }
            
            // 7. Verificar que los logs se escribieron
            Console.WriteLine("\n=== VERIFICACIÓN ===\n");
            Console.WriteLine($"Archivo de log: {fileLogger.GetCurrentLogFilePath()}");
            Console.WriteLine("\nAbre el archivo de log para verificar que todos los mensajes se guardaron.");
            Console.WriteLine("Presiona Enter para salir...");
            Console.ReadLine();
        }
    }
}
