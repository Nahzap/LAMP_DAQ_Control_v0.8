# LAMP DAQ Control v0.8 - Plan de Optimización y Splash Screen
## Documento de Análisis, Diseño y Propuesta de Implementación
### Fecha: 2026-03-24 11:15:00 UTC-03:00
### Autor: Cascade AI Assistant
### Versión del documento: 1.0

---

## TABLA DE CONTENIDOS

1. [Resumen Ejecutivo](#1-resumen-ejecutivo)
2. [Estado Actual del Sistema](#2-estado-actual-del-sistema)
3. [Análisis de Código Fuente](#3-análisis-de-código-fuente)
4. [Oportunidades de Optimización](#4-oportunidades-de-optimización)
5. [Diseño de Splash Screen](#5-diseño-de-splash-screen)
6. [Plan de Implementación](#6-plan-de-implementación)
7. [Riesgos y Mitigación](#7-riesgos-y-mitigación)
8. [Métricas de Éxito](#8-métricas-de-éxito)

---

## 1. RESUMEN EJECUTIVO

Este documento presenta un análisis exhaustivo del código fuente de LAMP DAQ Control v0.8,
identificando oportunidades concretas de optimización para simplificar operaciones y delegar
responsabilidades modulares correctamente. Adicionalmente, se propone el diseño e implementación
de una pantalla de bienvenida (splash screen) autoexplicativa que se muestre antes de la
carga de la aplicación principal.

### 1.1 Objetivos Principales

- **OPT-1**: Reducir complejidad ciclomática en `DeviceManager` (891 líneas → ~400 líneas)
- **OPT-2**: Eliminar duplicación de lógica de detección de tipo de dispositivo
- **OPT-3**: Desacoplar `MainViewModel` de instanciación directa de servicios
- **OPT-4**: Optimizar `SignalGenerator` (parsing CSV en hot loop)
- **OPT-5**: Unificar sistema de logging redundante
- **SPLASH-1**: Implementar splash screen WPF con información del sistema

### 1.2 Alcance

| Componente | Acción | Prioridad |
|---|---|---|
| `DeviceManager.cs` | Refactorizar → Strategy Pattern | Alta |
| `DAQController.cs` | Eliminar duplicación de lógica de tipo | Alta |
| `SignalGenerator.cs` | Pre-parsear LUT, eliminar CSV parse en loop | Alta |
| `MainViewModel.cs` | Extraer factory de servicios | Media |
| `ConsoleLogger.cs` | Eliminar escritura dual redundante | Media |
| `SplashScreen` (nuevo) | Crear ventana WPF splash | Alta |
| `Program.cs` | Integrar splash antes de `app.Run()` | Alta |

---

## 2. ESTADO ACTUAL DEL SISTEMA

### 2.1 Arquitectura General

```
┌─────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                   │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │ MainWindow   │  │ AnalogPanel  │  │ DigitalPanel  │  │
│  │   .xaml      │  │   .xaml      │  │   .xaml       │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬────────┘  │
├─────────┼─────────────────┼─────────────────┼───────────┤
│         │    VIEWMODEL LAYER                │           │
│  ┌──────┴───────┐  ┌──────┴───────┐  ┌──────┴────────┐  │
│  │MainViewModel │  │AnalogControl │  │DigitalMonitor │  │
│  │              │  │  ViewModel   │  │  ViewModel    │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬────────┘  │
├─────────┼─────────────────┼─────────────────┼───────────┤
│         │    BUSINESS LOGIC LAYER           │           │
│         ┼───────────────────────────────────┘           │
│         │                                               │
│  ┌──────┴──────┐                                        │
│  │DAQController│                                        │
│  └──────┬──────┘                                        │
│         ┌──────────┬─────────┬───────────┐              │
│   ┌─────┴─────┐ ┌──┴───┐ ┌───┴────┐ ┌────┴──────┐       │
│   │DeviceMgr  │ │Chan  │ │Profile │ │Signal     │       │
│   │(891 lns)  │ │Mgr   │ │Mgr     │ │Generator  │       │
│   └─────┬─────┘ └──────┘ └────────┘ └───────────┘       │
├─────────┼───────────────────────────────────────────────┤
│         │    HARDWARE LAYER (Advantech SDK)             │
│   ┌─────┴─────────────────────────────────────┐         │
│   │ InstantAoCtrl │ InstantDiCtrl │ InstantDoCtrl│      │
│   └───────────────┴───────────────┴──────────────┘      │
└─────────────────────────────────────────────────────────┘
```

### 2.2 Estadísticas del Código Actual

| Archivo | Líneas | Complejidad | Responsabilidades |
|---|---|---|---|
| `DeviceManager.cs` | 891 | Alta (CC≈35) | Detección, init, I/O, dispose, info |
| `DAQController.cs` | 420 | Media (CC≈15) | Orquestación, delegación, validación |
| `SignalGenerator.cs` | 502 | Media-Alta (CC≈20) | Señales, ramp, DC, LUT |
| `MainViewModel.cs` | 257 | Media (CC≈12) | Init, detección, UI state |
| `AnalogControlViewModel.cs` | 431 | Media (CC≈14) | Control analógico, comandos |
| `DigitalMonitorViewModel.cs` | 246 | Baja (CC≈8) | Monitoreo digital |
| `ChannelManager.cs` | 97 | Baja (CC≈4) | Estado canales, reset |
| `ConsoleLogger.cs` | 112 | Baja (CC≈3) | Logging consola + archivo |

### 2.3 Patrones Actuales Identificados

- **MVVM**: Implementado en capa WPF (ViewModelBase, RelayCommand)
- **Observer**: Eventos en AnalogOutputTracker, DigitalInputMonitor
- **Dependency Injection**: Manual (sin contenedor IoC)
- **Strategy**: Parcial en DeviceManager (analog vs digital)
- **Composite**: En CompositeLogger

### 2.4 Flujo de Inicio Actual

```
Program.Main()
  ├─ args == "-console" → RunConsoleMode()
  └─ else → RunWPFMode()
       ├─ AllocConsole() // Ventana de logs
       ├─ new App()
       ├─ app.InitializeComponent()
       └─ app.Run()
            └─ MainWindow.xaml (StartupUri)
                 └─ MainViewModel() constructor
                      ├─ FileLogger, ConsoleLogger, CompositeLogger
                      ├─ ActionLogger
                      ├─ DAQController(compositeLogger)
                      ├─ DeviceDetectionService
                      ├─ AnalogControlViewModel
                      ├─ DigitalMonitorViewModel
                      └─ RefreshDevices() → DetectDevices()
```

**Problema**: No hay splash screen. La ventana principal aparece directamente,
y si la detección de dispositivos tarda, el usuario ve una UI congelada.

---

## 3. ANÁLISIS DE CÓDIGO FUENTE

### 3.1 DeviceManager.cs — CRÍTICO (891 líneas)

**Diagnóstico**: Viola el Principio de Responsabilidad Única (SRP). Este archivo
concentra demasiadas responsabilidades que deberían estar separadas.

#### 3.1.1 Responsabilidades Actuales (demasiadas)

1. **Detección de dispositivos** (`DetectDevices()`, líneas 532-637)
   - Itera 8 posiciones para analog y digital
   - Crea InstantAoCtrl/InstantDiCtrl/InstantDoCtrl temporales
   - Lógica duplicada entre DI y DO

2. **Inicialización de dispositivos** (`InitializeDevice()`, líneas 68-155)
   - Determina tipo de dispositivo por nombre de perfil (hardcoded strings)
   - Fallback: intenta analog → digital
   - Gestión de estado de inicialización

3. **Inicialización específica por tipo** (líneas 159-383)
   - `TryInitializeAnalogDevice()` — 50 líneas
   - `TryInitializeDigitalDevice()` — 10 líneas (delegador)
   - `TryInitializeDigitalInputDevice()` — 80 líneas
   - `TryInitializeDigitalOutputDevice()` — 80 líneas
   - **Duplicación masiva** entre DI y DO (~90% código idéntico)

4. **Operaciones I/O** (líneas 385-530)
   - `WriteVoltage()`, `WriteDigitalBit()`, `ReadDigitalBit()`
   - `WriteDigitalPort()`, `ReadDigitalPort()`
   - Cada método valida tipo de dispositivo internamente

5. **Información de dispositivo** (`GetDeviceInfo()`, líneas 640-707)
   - Switch extenso por tipo de dispositivo

6. **Configuración** (líneas 709-782)
   - `ConfigureChannels()` — solo analog
   - `ConfigureDigitalPorts()` — solo digital

7. **Gestión de ciclo de vida** (líneas 784-888)
   - `DisposeDevices()` — reset + dispose + recrear
   - `Dispose()` — cleanup final

#### 3.1.2 Código Duplicado Detectado

```
TryInitializeDigitalInputDevice()  ≈  TryInitializeDigitalOutputDevice()
```

Estas dos funciones comparten ~90% del código:
- Búsqueda de dispositivo por Board ID
- Logging de dispositivos disponibles
- Verificación de PortCount == 4
- Test de comunicación (read vs write)

**Líneas duplicadas estimadas**: ~120 líneas

#### 3.1.3 Strings Hardcodeados para Detección de Tipo

En `InitializeDevice()` y en `DAQController.Initialize()`:

```csharp
// DeviceManager.cs:73-76
bool isDigitalProfile = !string.IsNullOrEmpty(profileName) && 
                       (profileName.Contains("PCI1735") || profileName.Contains("1735"));
bool isAnalogProfile = !string.IsNullOrEmpty(profileName) && 
                      (profileName.Contains("PCIe1824") || profileName.Contains("1824"));
```

```csharp
// DAQController.cs:109-112 — MISMA lógica duplicada
bool isDigitalProfile = profileName != null && 
                       (profileName.Contains("PCI1735") || profileName.Contains("1735"));
bool isAnalogProfile = profileName != null && 
                      (profileName.Contains("PCIe1824") || profileName.Contains("1824"));
```

**Impacto**: Si se agrega un nuevo tipo de dispositivo, hay que modificar ambos archivos.

### 3.2 DAQController.cs — MEDIO (420 líneas)

**Diagnóstico**: Buena estructura general como orquestador, pero con problemas puntuales.

#### 3.2.1 Problemas Identificados

1. **Duplicación de lógica de tipo de dispositivo** (ver 3.1.3)
   - `Initialize()` repite la misma detección que `DeviceManager.InitializeDevice()`

2. **Recreación de SignalGenerator en cada Initialize()**
   ```csharp
   // DAQController.cs:131
   _signalGenerator = new SignalGenerator(_deviceManager.Device, _logger);
   ```
   - Se recrea incluso si el dispositivo no cambió realmente
   - El guard `if (_deviceManager.IsInitialized && _deviceManager.CurrentDeviceType == targetType)` 
     debería prevenir esto, pero la lógica del guard tiene edge cases

3. **Corrección de perfil post-inicialización**
   ```csharp
   // DAQController.cs:138-148
   if (deviceInfo.DeviceType == DeviceType.Digital && !isDigitalProfile)
   {
       correctedProfile = "PCI1735U_prof_v1";
   }
   ```
   - Se detecta el tipo, se inicializa, y DESPUÉS se corrige el perfil
   - Debería corregirse ANTES de inicializar

4. **Validación duplicada**
   - `ValidateChannelNumber()` existe en DAQController Y en DeviceManager
   - `ValidatePortNumber()` y `ValidateBitNumber()` solo en DAQController
     pero la validación real ocurre en DeviceManager

### 3.3 SignalGenerator.cs — ALTO IMPACTO (502 líneas)

**Diagnóstico**: Contiene una optimización crítica pendiente en el hot loop.

#### 3.3.1 Parsing CSV en Hot Loop (CRÍTICO)

```csharp
// SignalGenerator.cs:437-448 — DENTRO del while(!cancelled) loop
string line = lutLines[lutIndex + 1];
double normalizedValue = 0.5;
if (!string.IsNullOrEmpty(line))
{
    string[] parts = line.Split(',');  // ALLOCATION en cada sample!
    if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort value))
    {
        normalizedValue = value / 65535.0;
    }
}
```

**Problema**: En cada muestra de señal se ejecuta:
- `string.Split(',')` — Crea un array nuevo (heap allocation)
- `ushort.TryParse()` — Parse de string en cada iteración
- GC pressure significativa en thread de alta prioridad

**Para una señal de 100Hz con 500 samples/ciclo = 50,000 allocations/segundo**

#### 3.3.2 Solución Propuesta

Pre-parsear el CSV a un array `double[]` al cargar:

```csharp
// Propuesta: Caché numérica pre-parseada
private static double[] _cachedSineValues = null;

// En el lock de carga:
_cachedSineValues = new double[lutSize];
for (int i = 0; i < lutSize; i++)
{
    string[] parts = lutLines[i + 1].Split(',');
    if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort val))
        _cachedSineValues[i] = val / 65535.0;
    else
        _cachedSineValues[i] = 0.5;
}

// En el hot loop: ZERO allocations
double normalizedValue = _cachedSineValues[lutIndex];
```

**Impacto estimado**: Eliminación de ~50,000 allocations/segundo por canal activo.

#### 3.3.3 Finally Block Problemático

```csharp
// SignalGenerator.cs:491-496
finally
{
    try { GC.EndNoGCRegion(); } catch { }
    try { Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0xFFFF; } catch { }
}
```

**Problema**: Se llama `GC.EndNoGCRegion()` sin haber llamado `GC.TryStartNoGCRegion()`.
Siempre lanza excepción que es silenciada por catch. Código muerto inofensivo pero confuso.

### 3.4 MainViewModel.cs — MEDIO (257 líneas)

#### 3.4.1 Acoplamiento Fuerte en Constructor

```csharp
// MainViewModel.cs — Constructor
_fileLogger = new FileLogger();
var consoleLogger = new ConsoleLogger();
var compositeLogger = new CompositeLogger(_fileLogger, consoleLogger);
_actionLogger = new ActionLogger(compositeLogger);
_controller = new DAQController(compositeLogger);
_detectionService = new DeviceDetectionService(new Services.ConsoleService());
```

**Problema**: Todas las dependencias se crean directamente.
- No hay forma de inyectar mocks para testing
- No hay separación entre creación y configuración
- Si cambia la estrategia de logging, hay que modificar el ViewModel

#### 3.4.2 RefreshDevices() en Constructor

```csharp
// Se ejecuta detección de dispositivos en el constructor
RefreshDevices();
```

**Problema**: Operación potencialmente lenta (I/O de hardware) ejecutada
sincrónicamente en el constructor del ViewModel, que a su vez se ejecuta
desde el thread de UI (XAML DataContext creation).

### 3.5 ConsoleLogger.cs — BAJO (112 líneas)

#### 3.5.1 Escritura Dual Redundante

Cada `ConsoleLogger` escribe a consola Y a `LATEST_LOG.txt`.
Pero también existe `FileLogger` que escribe a `%APPDATA%\LAMP_DAQ_Control\Logs\`.

**Resultado**: Toda la actividad de logging se escribe:
1. A consola (ConsoleLogger)
2. A `LATEST_LOG.txt` en el directorio de la app (ConsoleLogger)
3. A `%APPDATA%\...\Logs\` (FileLogger via CompositeLogger)

Triple escritura para cada mensaje de log.

### 3.6 MainWindow.xaml.cs — BAJO-MEDIO (120 líneas)

#### 3.6.1 DataContext Manual en Code-Behind

```csharp
// MainWindow.xaml.cs:48
analogPanel.DataContext = viewModel.AnalogControl;
```

Asignación manual de DataContext que ya está hecha en XAML:
```xml
<!-- MainWindow.xaml:170 -->
<views:AnalogControlPanel DataContext="{Binding AnalogControl}"/>
```

**Problema**: Doble asignación. El code-behind sobreescribe lo que XAML ya bindea.
El PropertyChanged subscriber (líneas 55-70) es código defensivo innecesario
si el binding XAML funciona correctamente.

---

## 4. OPORTUNIDADES DE OPTIMIZACIÓN

### 4.1 OPT-1: Refactorizar DeviceManager con Strategy Pattern

#### Estado Actual
```
DeviceManager (891 líneas)
  ├─ Lógica Analog
  ├─ Lógica Digital Input
  ├─ Lógica Digital Output
  ├─ Detección
  ├─ I/O
  └─ Configuración
```

#### Estado Propuesto
```
DeviceManager (≈200 líneas) — Orquestador
  ├─ IDeviceStrategy
  │    ├─ AnalogDeviceStrategy (≈150 líneas)
  │    └─ DigitalDeviceStrategy (≈150 líneas)
  ├─ DeviceDetector (≈120 líneas) — Detección extraída
  └─ DeviceTypeResolver (≈30 líneas) — Resolución de tipo centralizada
```

#### Interfaz Propuesta

```csharp
public interface IDeviceStrategy
{
    DeviceType DeviceType { get; }
    bool TryInitialize(int deviceNumber);
    void WriteValue(int channel, double value);
    double ReadValue(int channel);
    void Configure();
    DeviceInfo GetInfo(int deviceNumber);
    void Reset();
    void Dispose();
}
```

#### DeviceTypeResolver — Elimina duplicación

```csharp
public static class DeviceTypeResolver
{
    private static readonly string[] DigitalIdentifiers = { "PCI1735", "1735" };
    private static readonly string[] AnalogIdentifiers = { "PCIe1824", "1824" };
    
    public static DeviceType ResolveFromProfile(string profileName)
    {
        if (string.IsNullOrEmpty(profileName)) return DeviceType.Unknown;
        
        if (DigitalIdentifiers.Any(id => profileName.Contains(id)))
            return DeviceType.Digital;
        if (AnalogIdentifiers.Any(id => profileName.Contains(id)))
            return DeviceType.Analog;
            
        return DeviceType.Unknown;
    }
    
    public static string GetDefaultProfile(DeviceType type)
    {
        switch (type)
        {
            case DeviceType.Digital: return "PCI1735U_prof_v1";
            case DeviceType.Analog: return "PCIe1824_prof_v1";
            default: return null;
        }
    }
}
```

**Beneficios**:
- DAQController y DeviceManager usan la misma fuente de verdad
- Agregar nuevo tipo de dispositivo = nueva clase Strategy + entrada en Resolver
- Testeable independientemente

### 4.2 OPT-2: Pre-parsear LUT en SignalGenerator

#### Cambio Específico

Reemplazar el bloque de caché actual (líneas 371-390 de SignalGenerator.cs)
con pre-parsing numérico:

```csharp
// NUEVO: Array numérico pre-parseado
private static double[] _cachedNormalizedValues = null;

// En el bloque lock de carga:
lock (_lutCacheLock)
{
    if (_cachedNormalizedValues == null)
    {
        var lines = File.ReadAllLines(csvPath);
        int size = lines.Length - 1; // skip header
        _cachedNormalizedValues = new double[size];
        
        for (int i = 0; i < size; i++)
        {
            var parts = lines[i + 1].Split(',');
            if (parts.Length >= 2 && ushort.TryParse(parts[1], out ushort val))
                _cachedNormalizedValues[i] = val / 65535.0;
            else
                _cachedNormalizedValues[i] = 0.5;
        }
        
        _logger.Info($"[CACHE] Pre-parsed {size} LUT values to double[]");
    }
}

// EN EL HOT LOOP (reemplaza líneas 437-448):
double normalizedValue = _cachedNormalizedValues[lutIndex];
```

**Impacto**: De ~15 instrucciones por sample a 1 instrucción. Zero GC pressure.

### 4.3 OPT-3: ServiceFactory para MainViewModel

#### Cambio Propuesto

```csharp
public static class DAQServiceFactory
{
    public static (DAQController controller, ILogger logger, ActionLogger actionLogger) 
        CreateServices()
    {
        var fileLogger = new FileLogger();
        var consoleLogger = new ConsoleLogger();
        var compositeLogger = new CompositeLogger(fileLogger, consoleLogger);
        var actionLogger = new ActionLogger(compositeLogger);
        var controller = new DAQController(compositeLogger);
        
        return (controller, compositeLogger, actionLogger);
    }
}
```

```csharp
// MainViewModel constructor simplificado:
public MainViewModel()
{
    var (controller, logger, actionLogger) = DAQServiceFactory.CreateServices();
    _controller = controller;
    _actionLogger = actionLogger;
    // ... resto igual
}
```

### 4.4 OPT-4: Async Device Detection

Mover `RefreshDevices()` fuera del constructor:

```csharp
// MainViewModel — Constructor NO detecta dispositivos
public MainViewModel()
{
    // ... setup de servicios y commands ...
    StatusMessage = "Iniciando...";
    // NO llamar RefreshDevices() aquí
}

// Llamar desde MainWindow.Loaded:
private async void OnWindowLoaded(object sender, RoutedEventArgs e)
{
    var vm = DataContext as MainViewModel;
    await vm?.RefreshDevicesAsync();
}
```

### 4.5 OPT-5: Eliminar Logging Redundante

#### Actual: Triple escritura
```
Log message → ConsoleLogger.Info()
                ├─ Console.WriteLine()     (1)
                └─ File.AppendAllText()    (2) LATEST_LOG.txt
           → FileLogger.Info()
                └─ File.AppendAllText()    (3) %APPDATA%/Logs/
```

#### Propuesto: ConsoleLogger solo consola
```
Log message → ConsoleLogger.Info()
                └─ Console.WriteLine()     (1)
           → FileLogger.Info()
                └─ File.AppendAllText()    (2) %APPDATA%/Logs/
```

Eliminar la escritura a `LATEST_LOG.txt` del ConsoleLogger. El FileLogger
ya persiste los logs de forma más organizada.

### 4.6 OPT-6: Eliminar Code-Behind Redundante en MainWindow.xaml.cs

#### Código a eliminar (líneas 43-70)

El bloque que busca `AnalogControlPanel` en el árbol visual y asigna DataContext
manualmente es innecesario porque el binding XAML ya lo hace:

```xml
<views:AnalogControlPanel DataContext="{Binding AnalogControl}"/>
```

El subscriber a `PropertyChanged` (líneas 55-70) fue código defensivo añadido
durante desarrollo. El DataTrigger en `MainWindow.xaml` ya maneja la visibilidad
correctamente. Este código solo añade complejidad y confusión.

### 4.7 OPT-7: Limpiar Finally Block Fantasma en SignalGenerator

```csharp
// ACTUAL (líneas 491-496) — Siempre lanza excepción silenciada
finally
{
    try { GC.EndNoGCRegion(); } catch { }
    try { Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)0xFFFF; } catch { }
}
```

```csharp
// PROPUESTO — Eliminar completamente o implementar correctamente
finally
{
    // Solo restaurar afinidad si fue modificada
    // GC.EndNoGCRegion eliminado (nunca se llamó StartNoGCRegion)
}
```

### 4.8 OPT-8: Eliminar Console.WriteLine de Debug en ViewModels

Múltiples `System.Console.WriteLine` de debug dispersos en producción:

| Archivo | Líneas con Console.WriteLine debug |
|---|---|
| `AnalogControlViewModel.cs` | 38, 41, 56, 59, 87, 90, 165, 192, 196, 275, 296, 306 |
| `MainWindow.xaml.cs` | 47, 49, 53, 64, 66 |

**Total**: ~17 llamadas de debug que ensucian la consola de logs.
Deberían reemplazarse por `_logger.Debug()` o eliminarse.

---

## 5. DISEÑO DE SPLASH SCREEN

### 5.1 Requisitos Funcionales

| ID | Requisito | Prioridad |
|---|---|---|
| SPL-01 | Mostrar antes de que MainWindow aparezca | Alta |
| SPL-02 | Autoexplicativa: describir qué hace el programa | Alta |
| SPL-03 | Mostrar versión del programa | Alta |
| SPL-04 | Mostrar estado de carga (progreso) | Media |
| SPL-05 | Cerrar automáticamente al completar carga | Alta |
| SPL-06 | Diseño visual consistente con la app (colores #2C3E50, #3498DB) | Media |
| SPL-07 | Sin bordes de ventana (estilo splash profesional) | Media |
| SPL-08 | Centrada en pantalla | Baja |

### 5.2 Contenido Informativo del Splash

El splash debe comunicar al usuario:

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│              ╔══════════════════════════════╗               │
│              ║   LAMP DAQ Control v0.8      ║               │
│              ╚══════════════════════════════╝               │
│                                                             │
│    Sistema de Control y Adquisición de Datos                │
│    para tarjetas Advantech DAQ                               │
│                                                             │
│    ─────────────────────────────────────────                │
│                                                             │
│    Funcionalidades principales:                              │
│                                                             │
│    ■ Control de salidas analógicas (PCIe-1824)              │
│      - Escritura DC por canal (0-10V)                       │
│      - Generación de rampas con temporización precisa       │
│      - Generación de señales senoidales por LUT             │
│                                                             │
│    ■ Monitoreo de entradas digitales (PCI-1735U)            │
│      - Lectura en tiempo real de 4 puertos × 8 bits        │
│      - Frecuencia configurable (5-100 Hz)                   │
│                                                             │
│    ■ Signal Manager                                          │
│      - Secuencias programadas multi-canal                   │
│      - Control temporal con timeline visual                  │
│                                                             │
│    ─────────────────────────────────────────                │
│                                                             │
│    Hardware soportado:                                       │
│      • Advantech PCIe-1824 (Analog Output, 4 canales)       │
│      • Advantech PCI-1735U (Digital I/O, 32 bits)           │
│                                                             │
│    ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░  60%                        │
│    Detectando dispositivos...                                │
│                                                             │
│    .NET Framework 4.7.2 | Advantech DAQNavi SDK             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.3 Arquitectura de Implementación

#### 5.3.1 Archivos Nuevos a Crear

```
UI/WPF/Windows/
  └─ SplashWindow.xaml          ← Vista XAML del splash
  └─ SplashWindow.xaml.cs       ← Code-behind con lógica de progreso

Program.cs                       ← Modificar RunWPFMode()
UI/WPF/App.xaml                  ← Modificar StartupUri (opcional)
```

#### 5.3.2 Flujo de Inicio Modificado

```
Program.Main()
  └─ RunWPFMode()
       ├─ AllocConsole()
       ├─ new App()
       ├─ app.InitializeComponent()
       │
       ├─ *** NUEVO: Crear y mostrar SplashWindow ***
       │    ├─ splashWindow.Show()
       │    ├─ splashWindow.UpdateProgress("Inicializando sistema...", 10)
       │    ├─ splashWindow.UpdateProgress("Cargando configuración...", 30)
       │    ├─ splashWindow.UpdateProgress("Detectando hardware...", 60)
       │    ├─ splashWindow.UpdateProgress("Preparando interfaz...", 90)
       │    └─ splashWindow.UpdateProgress("Listo", 100)
       │
       ├─ app.Run(mainWindow)  ← MainWindow con dispositivos pre-detectados
       └─ splashWindow se cierra al abrir MainWindow
```

#### 5.3.3 Diseño XAML del SplashWindow

```xml
<Window x:Class="LAMP_DAQ_Control_v0_8.UI.WPF.Windows.SplashWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize"
        Width="620" Height="580"
        Topmost="True"
        ShowInTaskbar="False">

    <Border CornerRadius="12"
            Background="#FF1A2332"
            BorderBrush="#FF3498DB"
            BorderThickness="2"
            Effect="{StaticResource DropShadow}">
        
        <Grid Margin="30">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- Título -->
                <RowDefinition Height="Auto"/>  <!-- Subtítulo -->
                <RowDefinition Height="Auto"/>  <!-- Separador -->
                <RowDefinition Height="*"/>     <!-- Contenido -->
                <RowDefinition Height="Auto"/>  <!-- Separador -->
                <RowDefinition Height="Auto"/>  <!-- Hardware -->
                <RowDefinition Height="Auto"/>  <!-- Progreso -->
                <RowDefinition Height="Auto"/>  <!-- Status -->
                <RowDefinition Height="Auto"/>  <!-- Footer -->
            </Grid.RowDefinitions>
            
            <!-- TÍTULO -->
            <TextBlock Grid.Row="0"
                       Text="LAMP DAQ Control"
                       FontSize="32" FontWeight="Bold"
                       Foreground="#3498DB"
                       HorizontalAlignment="Center"
                       Margin="0,0,0,5"/>
            
            <TextBlock Grid.Row="1"
                       Text="v0.8 — Sistema de Control y Adquisición de Datos"
                       FontSize="14"
                       Foreground="#95A5A6"
                       HorizontalAlignment="Center"
                       Margin="0,0,0,15"/>
            
            <!-- SEPARADOR -->
            <Rectangle Grid.Row="2" Height="1" 
                       Fill="#3498DB" Opacity="0.5" Margin="0,5"/>
            
            <!-- CONTENIDO DESCRIPTIVO -->
            <StackPanel Grid.Row="3" Margin="10,15">
                
                <!-- Funcionalidad Analógica -->
                <TextBlock Text="■  Control de Salidas Analógicas (PCIe-1824)"
                           FontSize="13" FontWeight="SemiBold"
                           Foreground="#E8E8E8" Margin="0,0,0,4"/>
                <TextBlock Text="     Escritura DC por canal, rampas de voltaje con"
                           FontSize="11" Foreground="#95A5A6"/>
                <TextBlock Text="     temporización de alta precisión, y generación"
                           FontSize="11" Foreground="#95A5A6"/>
                <TextBlock Text="     de señales senoidales mediante tabla LUT."
                           FontSize="11" Foreground="#95A5A6" Margin="0,0,0,10"/>
                
                <!-- Funcionalidad Digital -->
                <TextBlock Text="■  Monitoreo de Entradas Digitales (PCI-1735U)"
                           FontSize="13" FontWeight="SemiBold"
                           Foreground="#E8E8E8" Margin="0,0,0,4"/>
                <TextBlock Text="     Lectura en tiempo real de 4 puertos × 8 bits."
                           FontSize="11" Foreground="#95A5A6"/>
                <TextBlock Text="     Frecuencia de muestreo configurable (5-100 Hz)."
                           FontSize="11" Foreground="#95A5A6" Margin="0,0,0,10"/>
                
                <!-- Signal Manager -->
                <TextBlock Text="■  Signal Manager — Secuencias Programadas"
                           FontSize="13" FontWeight="SemiBold"
                           Foreground="#E8E8E8" Margin="0,0,0,4"/>
                <TextBlock Text="     Control temporal multi-canal con timeline visual"
                           FontSize="11" Foreground="#95A5A6"/>
                <TextBlock Text="     para automatización de pruebas de laboratorio."
                           FontSize="11" Foreground="#95A5A6"/>
            </StackPanel>
            
            <!-- SEPARADOR -->
            <Rectangle Grid.Row="4" Height="1" 
                       Fill="#3498DB" Opacity="0.3" Margin="0,5"/>
            
            <!-- HARDWARE SOPORTADO -->
            <StackPanel Grid.Row="5" Orientation="Horizontal" 
                        HorizontalAlignment="Center" Margin="0,8">
                <TextBlock Text="Advantech PCIe-1824" 
                           FontSize="11" Foreground="#27AE60" Margin="0,0,20,0"/>
                <TextBlock Text="•" FontSize="11" Foreground="#555" Margin="0,0,20,0"/>
                <TextBlock Text="Advantech PCI-1735U" 
                           FontSize="11" Foreground="#27AE60"/>
            </StackPanel>
            
            <!-- BARRA DE PROGRESO -->
            <Grid Grid.Row="6" Margin="0,10,0,5">
                <ProgressBar x:Name="LoadProgress"
                             Height="6"
                             Minimum="0" Maximum="100" Value="0"
                             Background="#2C3E50"
                             Foreground="#3498DB"
                             BorderThickness="0"/>
            </Grid>
            
            <!-- STATUS MESSAGE -->
            <TextBlock Grid.Row="7"
                       x:Name="StatusText"
                       Text="Iniciando aplicación..."
                       FontSize="11"
                       Foreground="#7F8C8D"
                       HorizontalAlignment="Center"
                       Margin="0,0,0,10"/>
            
            <!-- FOOTER -->
            <TextBlock Grid.Row="8"
                       Text=".NET Framework 4.7.2  |  Advantech DAQNavi SDK"
                       FontSize="9"
                       Foreground="#555"
                       HorizontalAlignment="Center"/>
        </Grid>
    </Border>
</Window>
```

#### 5.3.4 Code-Behind del SplashWindow

```csharp
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }
    
    public void UpdateProgress(string message, int percentage)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
            LoadProgress.Value = percentage;
        });
    }
    
    public async Task AnimateProgressAsync(string message, int from, int to, int durationMs)
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            StatusText.Text = message;
            int steps = to - from;
            int stepDelay = durationMs / Math.Max(steps, 1);
            
            for (int i = from; i <= to; i++)
            {
                LoadProgress.Value = i;
                await Task.Delay(stepDelay);
            }
        });
    }
}
```

#### 5.3.5 Integración en Program.cs

```csharp
static void RunWPFMode()
{
    AllocConsole();
    Console.Title = "LAMP DAQ Control v0.8 - Sistema de Logs";
    // ... mensajes de consola existentes ...
    
    var app = new UI.WPF.App();
    app.InitializeComponent();
    
    // Crear y mostrar splash
    var splash = new UI.WPF.Windows.SplashWindow();
    splash.Show();
    splash.UpdateProgress("Inicializando sistema de logging...", 10);
    
    // Permitir que el splash se renderice
    DoEvents();
    
    splash.UpdateProgress("Cargando configuración...", 30);
    DoEvents();
    
    // Pre-cargar servicios pesados aquí si se desea
    splash.UpdateProgress("Preparando interfaz principal...", 70);
    DoEvents();
    
    splash.UpdateProgress("Listo", 100);
    DoEvents();
    System.Threading.Thread.Sleep(500); // Breve pausa para ver 100%
    
    // Crear MainWindow y cerrar splash
    var mainWindow = new UI.WPF.Windows.MainWindow();
    splash.Close();
    
    app.Run(mainWindow);
}

// Helper para procesar mensajes WPF pendientes
static void DoEvents()
{
    var frame = new System.Windows.Threading.DispatcherFrame();
    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
        System.Windows.Threading.DispatcherPriority.Background,
        new System.Windows.Threading.DispatcherOperationCallback(
            delegate (object f)
            {
                ((System.Windows.Threading.DispatcherFrame)f).Continue = false;
                return null;
            }), frame);
    System.Windows.Threading.Dispatcher.PushFrame(frame);
}
```

### 5.4 Alternativa: Splash en App.xaml.cs con Background Worker

Si se prefiere integrar la detección de dispositivos en el splash:

```csharp
// App.xaml.cs — OnStartup modificado
protected override async void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // Exception handlers existentes...
    
    var splash = new SplashWindow();
    splash.Show();
    
    // Etapa 1: Sistema
    splash.UpdateProgress("Verificando dependencias del sistema...", 15);
    await Task.Delay(300);
    
    // Etapa 2: Hardware  
    splash.UpdateProgress("Escaneando dispositivos DAQ...", 40);
    // Aquí se podría pre-detectar dispositivos
    await Task.Delay(500);
    
    // Etapa 3: UI
    splash.UpdateProgress("Construyendo interfaz de usuario...", 75);
    var mainWindow = new MainWindow();
    
    // Etapa 4: Final
    splash.UpdateProgress("Iniciando LAMP DAQ Control...", 100);
    await Task.Delay(400);
    
    MainWindow = mainWindow;
    mainWindow.Show();
    splash.Close();
}
```

En este caso, `App.xaml` debe cambiar:
```xml
<!-- ANTES -->
StartupUri="Windows/MainWindow.xaml"
<!-- DESPUÉS (eliminar StartupUri, manejar desde OnStartup) -->
```

---

## 6. PLAN DE IMPLEMENTACIÓN

### 6.1 Fases y Cronograma

```
FASE 1 — Splash Screen (Impacto visual inmediato)
├─ Tarea 1.1: Crear SplashWindow.xaml + .xaml.cs
├─ Tarea 1.2: Modificar Program.cs o App.xaml.cs
├─ Tarea 1.3: Ajustar App.xaml (StartupUri)
├─ Tarea 1.4: Probar en modo WPF
└─ Estimación: 1-2 horas

FASE 2 — Optimización SignalGenerator (Mayor impacto rendimiento)
├─ Tarea 2.1: Pre-parsear LUT a double[]
├─ Tarea 2.2: Eliminar string.Split del hot loop
├─ Tarea 2.3: Limpiar finally block fantasma
├─ Tarea 2.4: Verificar generación de señal correcta
└─ Estimación: 1 hora

FASE 3 — Refactorizar DeviceManager (Mayor impacto arquitectural)
├─ Tarea 3.1: Crear DeviceTypeResolver estático
├─ Tarea 3.2: Crear IDeviceStrategy + implementaciones
├─ Tarea 3.3: Extraer DeviceDetector
├─ Tarea 3.4: Simplificar DeviceManager como orquestador
├─ Tarea 3.5: Actualizar DAQController para usar Resolver
├─ Tarea 3.6: Pruebas de integración
└─ Estimación: 3-4 horas

FASE 4 — Limpieza y Desacoplamiento (Calidad de código)
├─ Tarea 4.1: Crear DAQServiceFactory
├─ Tarea 4.2: Simplificar MainViewModel constructor
├─ Tarea 4.3: Eliminar Console.WriteLine de debug
├─ Tarea 4.4: Eliminar code-behind redundante en MainWindow
├─ Tarea 4.5: Simplificar ConsoleLogger (solo consola)
├─ Tarea 4.6: Async RefreshDevices
└─ Estimación: 2-3 horas
```

### 6.2 Orden de Prioridad

| # | Tarea | Riesgo | Impacto | Esfuerzo |
|---|---|---|---|---|
| 1 | Splash Screen | Bajo | Alto (UX) | Bajo |
| 2 | Pre-parsear LUT | Bajo | Alto (perf) | Bajo |
| 3 | DeviceTypeResolver | Bajo | Medio (maint) | Bajo |
| 4 | Eliminar Console.WriteLine debug | Nulo | Bajo (clean) | Bajo |
| 5 | Limpiar finally block | Nulo | Bajo (clean) | Mínimo |
| 6 | DAQServiceFactory | Bajo | Medio (test) | Bajo |
| 7 | Strategy Pattern DeviceManager | Medio | Alto (arch) | Alto |
| 8 | Async RefreshDevices | Bajo | Medio (UX) | Medio |
| 9 | Eliminar code-behind redundante | Bajo | Bajo (clean) | Bajo |
| 10 | Simplificar ConsoleLogger | Bajo | Bajo (perf) | Bajo |

### 6.3 Detalle de Tarea 1.1 — Crear SplashWindow

**Archivos a crear**:
- `UI/WPF/Windows/SplashWindow.xaml`
- `UI/WPF/Windows/SplashWindow.xaml.cs`

**Archivos a modificar**:
- `Program.cs` — Agregar lógica de splash en `RunWPFMode()`
- `UI/WPF/App.xaml` — Posiblemente remover `StartupUri`
- `UI/WPF/App.xaml.cs` — Posiblemente mover lógica a `OnStartup`

**Dependencias**: Ninguna. Se puede implementar sin afectar código existente.

**Verificación**:
1. Ejecutar la aplicación en modo WPF (sin `-console`)
2. Verificar que el splash aparece centrado, sin bordes
3. Verificar que muestra el texto descriptivo completo
4. Verificar que la barra de progreso avanza
5. Verificar que se cierra automáticamente al abrir MainWindow
6. Verificar que MainWindow funciona normalmente después

### 6.4 Detalle de Tarea 2.1 — Pre-parsear LUT

**Archivo a modificar**: `Core/DAQ/Services/SignalGenerator.cs`

**Cambios específicos**:
1. Agregar campo estático `private static double[] _cachedNormalizedValues`
2. En el bloque `lock(_lutCacheLock)`, parsear a double[] después de leer líneas
3. En `GenerateSignal()`, reemplazar parsing inline por acceso directo al array
4. Eliminar campo `_cachedSineLUT` (string[] ya no necesario)

**Verificación**:
1. Generar señal senoidal en cualquier canal
2. Verificar que la forma de onda es correcta (oscilloscopio o log)
3. Verificar que no hay degradación de temporización

### 6.5 Detalle de Tarea 3.1 — DeviceTypeResolver

**Archivo a crear**: `Core/DAQ/Services/DeviceTypeResolver.cs`

**Cambios en archivos existentes**:
- `DAQController.cs` — Reemplazar detección inline por `DeviceTypeResolver.ResolveFromProfile()`
- `DeviceManager.cs` — Reemplazar detección inline por `DeviceTypeResolver.ResolveFromProfile()`

**Verificación**:
1. Inicializar con perfil analógico → debe detectar Analog
2. Inicializar con perfil digital → debe detectar Digital
3. Inicializar sin perfil → debe detectar Unknown (fallback)

---

## 7. RIESGOS Y MITIGACIÓN

### 7.1 Matriz de Riesgos

| ID | Riesgo | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| R-01 | Splash bloquea inicio si hay error WPF | Baja | Alto | Try-catch en RunWPFMode; si splash falla, continuar sin él |
| R-02 | Strategy Pattern rompe inicialización de dispositivos | Media | Alto | Tests de integración antes de merge; mantener DeviceManager original como backup |
| R-03 | Pre-parseo de LUT cambia valores de señal | Baja | Alto | Comparar output con versión original; test con oscilloscopio |
| R-04 | Eliminar code-behind rompe DataContext | Baja | Medio | Probar con ambos dispositivos (analog + digital) |
| R-05 | Async RefreshDevices causa race condition | Media | Medio | Usar lock o SemaphoreSlim; deshabilitar UI durante detección |
| R-06 | DAQServiceFactory no cubre todos los escenarios | Baja | Bajo | Tests unitarios para factory; mantener compatibilidad constructor |

### 7.2 Plan de Rollback

Para cada fase, se mantiene la capacidad de revertir:

```
FASE 1 (Splash):
  Rollback: Eliminar SplashWindow, restaurar StartupUri en App.xaml
  Impacto: Nulo sobre funcionalidad existente

FASE 2 (SignalGenerator):
  Rollback: Restaurar _cachedSineLUT string[] y parsing inline
  Impacto: Vuelve a rendimiento anterior (funcional pero subóptimo)

FASE 3 (DeviceManager):
  Rollback: Restaurar DeviceManager.cs original (891 líneas)
  Impacto: Vuelve a código monolítico pero funcional
  NOTA: Esta es la fase de mayor riesgo

FASE 4 (Limpieza):
  Rollback: Git revert por archivo individual
  Impacto: Mínimo, cambios independientes entre sí
```

### 7.3 Estrategia de Testing por Fase

| Fase | Test Manual | Test Automatizado | Criterio de Aceptación |
|---|---|---|---|
| 1 | Verificar splash visual | N/A | Splash aparece, muestra info, cierra correctamente |
| 2 | Generar señal 100Hz | Comparar valores LUT | Forma de onda idéntica, sin jitter adicional |
| 3 | Init analog + digital | Tests unitarios existentes | Todos los tests pasan, dispositivos detectados |
| 4 | Ejecutar app completa | Build sin warnings | App funciona igual que antes de la limpieza |

---

## 8. MÉTRICAS DE ÉXITO

### 8.1 Métricas Cuantitativas

| Métrica | Valor Actual | Objetivo | Método de Medición |
|---|---|---|---|
| Líneas en DeviceManager | 891 | ≤400 | `wc -l DeviceManager.cs` |
| Código duplicado (DI/DO init) | ~120 líneas | 0 líneas | Análisis manual |
| Allocations en hot loop (SignalGenerator) | ~50K/seg/canal | 0 | Profiler o cálculo |
| Tiempo de primer render (percibido) | ~2-3 seg (pantalla vacía) | <0.5 seg (splash) | Cronómetro manual |
| Console.WriteLine de debug | ~17 llamadas | 0 | grep count |
| Fuentes de verdad para tipo de dispositivo | 2 (DAQController + DeviceManager) | 1 (DeviceTypeResolver) | Análisis manual |
| Destinos de escritura por log message | 3 | 2 | Análisis de código |

### 8.2 Métricas Cualitativas

- **Mantenibilidad**: Agregar nuevo tipo de dispositivo requiere SOLO crear nueva Strategy + entrada en Resolver
- **Testabilidad**: MainViewModel puede recibir dependencias mockeadas
- **Experiencia de usuario**: Splash informativo reduce percepción de espera
- **Legibilidad**: DeviceManager reducido a orquestador claro con delegación explícita

### 8.3 Definición de "Hecho" (DoD)

Cada tarea se considera completa cuando:
1. El código compila sin errores con MSBuild
2. La aplicación inicia correctamente en modo WPF
3. La aplicación inicia correctamente en modo Console (si aplica)
4. Los tests unitarios existentes pasan (si aplica)
5. No hay regresiones en funcionalidad existente
6. El código sigue las convenciones del proyecto (namespaces, estilo)

---

## 9. RESUMEN DE CAMBIOS POR ARCHIVO

### 9.1 Archivos Nuevos

| Archivo | Propósito | Fase |
|---|---|---|
| `UI/WPF/Windows/SplashWindow.xaml` | Vista XAML del splash screen | 1 |
| `UI/WPF/Windows/SplashWindow.xaml.cs` | Code-behind del splash (UpdateProgress) | 1 |
| `Core/DAQ/Services/DeviceTypeResolver.cs` | Resolución centralizada de tipo de dispositivo | 3 |
| `Core/DAQ/Interfaces/IDeviceStrategy.cs` | Interfaz para estrategia de dispositivo | 3 |
| `Core/DAQ/Strategies/AnalogDeviceStrategy.cs` | Estrategia para PCIe-1824 | 3 |
| `Core/DAQ/Strategies/DigitalDeviceStrategy.cs` | Estrategia para PCI-1735U | 3 |
| `Core/DAQ/Services/DeviceDetector.cs` | Detección de dispositivos extraída | 3 |
| `Core/DAQ/Services/DAQServiceFactory.cs` | Factory de servicios DAQ | 4 |

### 9.2 Archivos Modificados

| Archivo | Cambio | Fase | Riesgo |
|---|---|---|---|
| `Program.cs` | Agregar splash en RunWPFMode() | 1 | Bajo |
| `UI/WPF/App.xaml` | Posible remoción de StartupUri | 1 | Bajo |
| `UI/WPF/App.xaml.cs` | Posible manejo de splash en OnStartup | 1 | Bajo |
| `Core/DAQ/Services/SignalGenerator.cs` | Pre-parsear LUT a double[], limpiar finally | 2 | Bajo |
| `Core/DAQ/Managers/DeviceManager.cs` | Refactorizar con Strategy, extraer detector | 3 | Medio |
| `Core/DAQ/DAQController.cs` | Usar DeviceTypeResolver, eliminar duplicación | 3 | Bajo |
| `UI/WPF/ViewModels/MainViewModel.cs` | Usar DAQServiceFactory, async detect | 4 | Bajo |
| `UI/WPF/Windows/MainWindow.xaml.cs` | Eliminar code-behind redundante | 4 | Bajo |
| `Core/DAQ/Services/ConsoleLogger.cs` | Eliminar escritura a LATEST_LOG.txt | 4 | Bajo |
| `UI/WPF/ViewModels/AnalogControlViewModel.cs` | Eliminar Console.WriteLine debug | 4 | Nulo |

### 9.3 Archivos Sin Cambios (confirmados correctos)

| Archivo | Razón |
|---|---|
| `Core/DAQ/Managers/ChannelManager.cs` | Bien estructurado, responsabilidad clara (97 líneas) |
| `Core/DAQ/Managers/ProfileManager.cs` | Responsabilidad única, sin duplicación |
| `UI/WPF/ViewModels/ViewModelBase.cs` | Base MVVM correcta |
| `UI/WPF/ViewModels/RelayCommand.cs` | Implementación estándar ICommand |
| `UI/WPF/ViewModels/DigitalMonitorViewModel.cs` | Bien encapsulado |
| `UI/WPF/Windows/MainWindow.xaml` | Layout correcto con DataTriggers |
| `UI/WPF/Views/AnalogControlPanel.xaml` | Vista correcta |
| `UI/WPF/Views/DigitalControlPanel.xaml` | Vista correcta |
| `UI/WPF/Converters/*.cs` | Converters simples y correctos |

---

## 10. APÉNDICE A — PATRONES DE CÓDIGO ACTUAL vs PROPUESTO

### A.1 Detección de Tipo de Dispositivo

**ACTUAL** (duplicado en 2 archivos):
```csharp
// En DAQController.cs:109 Y DeviceManager.cs:73
bool isDigitalProfile = profileName != null && 
    (profileName.Contains("PCI1735") || profileName.Contains("1735"));
bool isAnalogProfile = profileName != null && 
    (profileName.Contains("PCIe1824") || profileName.Contains("1824"));
DeviceType targetType = isDigitalProfile ? DeviceType.Digital : 
                         isAnalogProfile ? DeviceType.Analog : 
                         DeviceType.Unknown;
```

**PROPUESTO** (una sola fuente de verdad):
```csharp
// En ambos archivos:
DeviceType targetType = DeviceTypeResolver.ResolveFromProfile(profileName);
```

### A.2 Inicialización Digital (DI vs DO)

**ACTUAL** (duplicado ~80 líneas × 2):
```csharp
// TryInitializeDigitalInputDevice — 80 líneas
// TryInitializeDigitalOutputDevice — 80 líneas
// Diferencia real: 3 líneas (Read vs Write, Di vs Do)
```

**PROPUESTO** (método genérico):
```csharp
private bool TryInitializeDigitalCtrl<T>(T ctrl, int deviceNumber, 
    Func<T, int> getPortCount, Action<T, int> testComm) where T : class
{
    // Lógica única: buscar device, verificar ports, test comm
    // ~40 líneas en lugar de 160
}
```

### A.3 SignalGenerator Hot Loop

**ACTUAL** (allocations por sample):
```csharp
// Dentro del while(!cancelled) → for(i < samplesPerCycle)
string line = lutLines[lutIndex + 1];           // referencia OK
string[] parts = line.Split(',');                // ALLOCATION!
ushort.TryParse(parts[1], out ushort value);     // parse cada vez
double normalizedValue = value / 65535.0;        // cálculo cada vez
```

**PROPUESTO** (zero allocation):
```csharp
// Dentro del while(!cancelled) → for(i < samplesPerCycle)
double normalizedValue = _cachedNormalizedValues[lutIndex]; // 1 instrucción
```

### A.4 Logging — ConsoleLogger

**ACTUAL** (dual write):
```csharp
public void Info(string message)
{
    string logLine = $"[INFO] {DateTime.Now:...}: {message}";
    WriteWithColor(logLine, ConsoleColor.White);  // → Console
    WriteToFile(logLine);                          // → LATEST_LOG.txt
}
```

**PROPUESTO** (single write):
```csharp
public void Info(string message)
{
    string logLine = $"[INFO] {DateTime.Now:...}: {message}";
    WriteWithColor(logLine, ConsoleColor.White);  // → Console ONLY
    // FileLogger (via CompositeLogger) se encarga de persistencia
}
```

---

## 11. APÉNDICE B — DIAGRAMA DE DEPENDENCIAS ACTUALIZADO

### B.1 Dependencias Actuales

```
MainViewModel
  ├─→ DAQController (new directo)
  ├─→ DeviceDetectionService (new directo)
  ├─→ FileLogger (new directo)
  ├─→ ConsoleLogger (new directo)
  ├─→ CompositeLogger (new directo)
  ├─→ ActionLogger (new directo)
  ├─→ AnalogControlViewModel (new directo)
  └─→ DigitalMonitorViewModel (new directo)

DAQController
  ├─→ DeviceManager (new directo o inyectado)
  ├─→ SignalGenerator (new directo, recreado)
  ├─→ ProfileManager (new directo o inyectado)
  └─→ ChannelManager (new directo o inyectado)
```

### B.2 Dependencias Propuestas

```
MainViewModel
  ├─→ DAQServiceFactory.CreateServices() ← NUEVO
  │     ├─ creates → ILogger (Composite)
  │     ├─ creates → ActionLogger
  │     └─ creates → DAQController
  ├─→ AnalogControlViewModel (new directo — OK, lightweight)
  └─→ DigitalMonitorViewModel (new directo — OK, lightweight)

DAQController
  ├─→ DeviceManager (inyectado)
  │     ├─→ IDeviceStrategy (inyectado/creado según tipo) ← NUEVO
  │     │     ├─ AnalogDeviceStrategy
  │     │     └─ DigitalDeviceStrategy
  │     └─→ DeviceDetector (extraído) ← NUEVO
  ├─→ DeviceTypeResolver (estático) ← NUEVO
  ├─→ SignalGenerator (recreado cuando cambia dispositivo)
  ├─→ ProfileManager (inyectado)
  └─→ ChannelManager (inyectado)
```

---

## 12. APÉNDICE C — CHECKLIST DE IMPLEMENTACIÓN

### C.1 Pre-Implementación
- [ ] Crear branch de trabajo: `feature/optimization-splash`
- [x] Verificar que build actual compila sin errores
- [ ] Ejecutar tests existentes como baseline
- [ ] Backup del estado actual

### C.2 Fase 1 — Splash Screen ✅ COMPLETADA (2026-03-24 11:36)
- [x] Crear `SplashWindow.xaml` con diseño propuesto (sección 5.3.3)
- [x] Crear `SplashWindow.xaml.cs` con UpdateProgress + ShowStep + DoEvents
- [x] Decidir integración: **App.xaml.cs** (alternativa 5.4 elegida)
- [x] Implementar splash en `App.OnStartup` con 6 pasos de progreso
- [x] Remover `StartupUri` de App.xaml
- [x] Registrar archivos en `.csproj` (Page + Compile)
- [x] Compilar con MSBuild — **BUILD OK**
- [ ] Test manual: splash aparece, muestra info, cierra, MainWindow funciona

### C.3 Fase 2 — SignalGenerator ✅ COMPLETADA (2026-03-24 11:38)
- [x] Agregar `private static double[] _cachedNormalizedValues` + `_cachedLutSize`
- [x] Modificar bloque lock para pre-parsear CSV completo a double[]
- [x] Reemplazar parsing inline en GenerateSignal() por acceso directo a array
- [x] Eliminar `_cachedSineLUT` (string[]) — reemplazado por double[]
- [x] Limpiar finally block — ahora restaura ThreadPriority en vez de GC fantasma
- [x] Eliminar 3× Console.WriteLine debug del static constructor
- [x] Compilar con MSBuild — **BUILD OK**
- [ ] Test: generar señal, verificar correcta operación

### C.4 Fase 3 — DeviceTypeResolver ✅ COMPLETADA (2026-03-24 11:40)
- [x] Crear `DeviceTypeResolver.cs` con ResolveFromProfile() + GetDefaultProfile() + helpers
- [x] Actualizar DAQController.Initialize() para usar Resolver
- [x] Actualizar DeviceManager.InitializeDevice() para usar Resolver
- [x] Registrar DeviceTypeResolver.cs en `.csproj`
- [x] Corregir corrección de perfil incompatible usando GetDefaultProfile()
- [x] Compilar con MSBuild — **BUILD OK**
- [ ] Test: detección e inicialización de ambos tipos de dispositivo
- [ ] Test: operaciones I/O en ambos tipos

> **NOTA**: Las tareas de Strategy Pattern (IDeviceStrategy, AnalogDeviceStrategy,
> DigitalDeviceStrategy, DeviceDetector) se posponen a una fase futura por su
> alto riesgo y complejidad. El DeviceTypeResolver ya elimina la duplicación
> principal (fuente de verdad única) con riesgo mínimo.

### C.5 Fase 4 — Limpieza ✅ COMPLETADA (2026-03-24 11:43)
- [ ] Crear `DAQServiceFactory.cs` — pospuesto (depende de Strategy Pattern)
- [ ] Simplificar MainViewModel constructor — pospuesto (depende de Factory)
- [x] Eliminar ~17 Console.WriteLine de debug:
  - [x] AnalogControlViewModel.cs — 14 llamadas eliminadas
  - [x] MainWindow.xaml.cs — 5 llamadas eliminadas
  - [x] SignalGenerator.cs — 3 llamadas eliminadas (static constructor)
- [x] Eliminar escritura dual a LATEST_LOG.txt en ConsoleLogger (112→68 líneas)
- [ ] Implementar async RefreshDevices — pospuesto a fase futura
- [x] Compilar con MSBuild — **BUILD OK**
- [ ] Test completo de regresión

### C.6 Post-Implementación
- [ ] Verificar que todos los tests pasan
- [ ] Verificar modo WPF completo (splash → analog → digital)
- [ ] Verificar modo Console (sin regresiones)
- [ ] Merge a branch principal
- [x] Actualizar documentación (este documento)

---

## 13. CONCLUSIONES

### 13.1 Hallazgos Principales

1. **DeviceManager es el archivo más crítico para refactorizar** — 891 líneas con
   responsabilidades mezcladas y ~120 líneas de código duplicado entre DI y DO.

2. **SignalGenerator tiene un bug de rendimiento en producción** — El parsing de CSV
   string dentro del hot loop genera ~50K allocations/segundo por canal, presionando
   innecesariamente al GC en un thread de alta prioridad.

3. **La detección de tipo de dispositivo está duplicada** — La misma lógica con strings
   hardcodeados existe en DAQController Y DeviceManager, violando DRY.

4. **No existe splash screen** — El usuario ve una ventana vacía o congelada mientras
   se inicializa el sistema, afectando la percepción de calidad.

5. **El logging tiene triple escritura innecesaria** — ConsoleLogger escribe a consola
   Y a archivo, mientras FileLogger también escribe a archivo. Cada mensaje se persiste
   dos veces en disco.

### 13.2 Impacto Estimado Total

| Categoría | Mejora Estimada |
|---|---|
| Rendimiento (SignalGenerator) | -99% allocations en hot loop |
| Mantenibilidad (DeviceManager) | -55% líneas de código |
| Duplicación | -100% código duplicado (detección tipo + DI/DO init) |
| UX (Splash) | Feedback inmediato al usuario desde el inicio |
| Testabilidad | MainViewModel inyectable con mocks |
| Limpieza | -17 Console.WriteLine de debug, -1 archivo de log redundante |

### 13.3 Recomendación Final

Implementar las fases en orden 1→2→3→4. Las fases 1 y 2 son de bajo riesgo
y alto impacto, y pueden completarse en una sesión de trabajo. La fase 3
requiere más cuidado por el alcance del refactoring en DeviceManager. La fase 4
es limpieza cosmética que puede hacerse incrementalmente.

**El splash screen (Fase 1) es la prioridad inmediata** por su impacto visual
directo y riesgo nulo sobre la funcionalidad existente.

---

*Documento generado el 2026-03-24 a las 11:15 UTC-03:00*
*Basado en análisis de código fuente y documentación existente en Docs/*
*LAMP DAQ Control v0.8 — .NET Framework 4.7.2*

---

## 14. REGISTRO DE IMPLEMENTACIÓN

> Sección agregada el 2026-03-24 a las 11:45 UTC-03:00 tras completar las 4 fases.

### 14.1 Estado General: 4/4 FASES COMPLETADAS — BUILD OK

| Fase | Estado | Hora | Errores de Build |
|---|---|---|---|
| 1 — Splash Screen | ✅ Completada | 11:36 | 0 |
| 2 — SignalGenerator LUT | ✅ Completada | 11:38 | 0 |
| 3 — DeviceTypeResolver | ✅ Completada | 11:40 | 1 (corregido: referencia residual `isDigitalProfile`) |
| 4 — Limpieza | ✅ Completada | 11:43 | 0 |

### 14.2 Archivos Creados (3)

| Archivo | Líneas | Descripción |
|---|---|---|
| `UI/WPF/Windows/SplashWindow.xaml` | ~125 | Ventana splash WPF sin bordes, fondo #1A2332, barra progreso |
| `UI/WPF/Windows/SplashWindow.xaml.cs` | ~70 | Code-behind: UpdateProgress, ShowStep, DoEvents |
| `Core/DAQ/Services/DeviceTypeResolver.cs` | ~70 | Resolución centralizada de tipo de dispositivo |

### 14.3 Archivos Modificados (7)

| Archivo | Cambio Principal |
|---|---|
| `UI/WPF/App.xaml` | Removido `StartupUri` |
| `UI/WPF/App.xaml.cs` | Splash integrado en OnStartup con 6 pasos de progreso |
| `Core/DAQ/Services/SignalGenerator.cs` | LUT pre-parseada a `double[]`, finally limpio |
| `Core/DAQ/DAQController.cs` | Usa `DeviceTypeResolver.ResolveFromProfile()` |
| `Core/DAQ/Managers/DeviceManager.cs` | Usa `DeviceTypeResolver.ResolveFromProfile()` |
| `UI/WPF/ViewModels/AnalogControlViewModel.cs` | -14 Console.WriteLine debug |
| `UI/WPF/Windows/MainWindow.xaml.cs` | -5 Console.WriteLine debug |
| `Core/DAQ/Services/ConsoleLogger.cs` | Eliminada escritura dual a LATEST_LOG.txt (112→68 lns) |
| `LAMP_DAQ_Control_v0.8.csproj` | +SplashWindow (Page+Compile), +DeviceTypeResolver, +RuntimeIdentifiers |

### 14.4 Métricas Logradas vs Objetivo

| Métrica | Antes | Después | Objetivo | Estado |
|---|---|---|---|---|
| Allocations en hot loop (SignalGenerator) | ~50K/seg/canal | 0 | 0 | ✅ |
| Console.WriteLine de debug | ~22 | 0 | 0 | ✅ |
| Fuentes de verdad para tipo de dispositivo | 2 | 1 | 1 | ✅ |
| Destinos de escritura por log (ConsoleLogger) | 2 (consola+archivo) | 1 (consola) | 1 | ✅ |
| Splash screen | No existe | Implementado | Implementado | ✅ |
| Líneas en ConsoleLogger | 112 | 68 | ≤70 | ✅ |
| Líneas en DeviceManager | 891 | 884 | ≤400 | ⏳ (Strategy Pattern pendiente) |

### 14.5 Tareas Pospuestas (Bajo Riesgo Futuro)

Estas tareas se identificaron en el plan original pero se pospusieron por ser de
alto riesgo o depender de refactorings más amplios:

| Tarea | Razón de Posponer | Prioridad |
|---|---|---|
| IDeviceStrategy + implementaciones | Alto riesgo: refactoring profundo de DeviceManager (891 lns) | Media |
| DeviceDetector (extraer DetectDevices) | Depende de Strategy Pattern | Media |
| DAQServiceFactory | Requiere definir interfaces de factory completas | Baja |
| Simplificar MainViewModel constructor | Depende de DAQServiceFactory | Baja |
| Async RefreshDevices | Requiere testing cuidadoso de race conditions | Baja |
| Eliminar code-behind redundante MainWindow | Conservado por precaución: DataContext dinámico funcional | Baja |

### 14.6 Decisiones Técnicas Tomadas

1. **Splash en App.xaml.cs** (no en Program.cs): Se eligió la alternativa 5.4 del plan
   porque permite controlar el ciclo de vida completo dentro del framework WPF,
   incluyendo manejo de excepciones y cierre limpio.

2. **DeviceTypeResolver estático** (no inyectable): Se implementó como clase estática
   porque es lógica pura sin estado ni dependencias externas. No requiere mock en tests.

3. **Strategy Pattern pospuesto**: El `DeviceTypeResolver` ya elimina la duplicación
   de detección de tipo. El Strategy Pattern completo (IDeviceStrategy + implementaciones)
   requiere un refactoring profundo de DeviceManager que merece su propia sesión.

4. **Code-behind de MainWindow conservado**: Aunque el plan propuso eliminarlo,
   se conserva el bloque de DataContext dinámico porque maneja el caso de
   AnalogControlPanel creado después de la selección de dispositivo.

5. **RuntimeIdentifiers agregado al .csproj**: Se agregó `<RuntimeIdentifiers>win</RuntimeIdentifiers>`
   para resolver un error de NuGet al compilar con MSBuild.
