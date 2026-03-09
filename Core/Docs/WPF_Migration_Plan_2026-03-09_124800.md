# Plan de Migración a Interfaz Visual WPF
## LAMP DAQ Control v0.8

**Fecha de creación**: 17 de Noviembre, 2025  
**Versión del documento**: 1.0  
**Estado**: En Planificación

---

## 📋 Tabla de Contenidos

1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Análisis de Arquitectura Actual](#análisis-de-arquitectura-actual)
3. [Requisitos del Sistema](#requisitos-del-sistema)
4. [Estrategia de Implementación](#estrategia-de-implementación)
5. [Estructura de Archivos Propuesta](#estructura-de-archivos-propuesta)
6. [Plan de Desarrollo por Fases](#plan-de-desarrollo-por-fases)
7. [Puntos de Integración](#puntos-de-integración)
8. [Riesgos y Mitigaciones](#riesgos-y-mitigaciones)
9. [Criterios de Aceptación](#criterios-de-aceptación)

---

## 1. Resumen Ejecutivo

### Objetivo
Migrar la aplicación LAMP DAQ Control de una interfaz de consola a una interfaz gráfica WPF con visualización en tiempo real de señales analógicas y digitales.

### Alcance
- **Dispositivos soportados**:
  - PCIE-1824 (32 canales AO - Solo escritura)
  - PCI-1735U (32 canales DI/DO configurables)
- **Funcionalidades principales**:
  - Control de salidas analógicas (DC, rampa, señales)
  - Monitoreo en tiempo real de entradas digitales
  - Visualización gráfica de comandos enviados (analógico)
  - Visualización gráfica de lecturas reales (digital)

### Tecnologías
- **Framework UI**: WPF (Windows Presentation Foundation)
- **Patrón de diseño**: MVVM (Model-View-ViewModel)
- **Librería de gráficos**: ScottPlot 4.1.68
- **Target Framework**: .NET Framework 4.7.2

---

## 2. Análisis de Arquitectura Actual

### 2.1 Estructura Actual

\\\
LAMP_DAQ_Control_v0.8/
├── Program.cs (Entry point)
├── Core/
│   └── DAQ/
│       ├── DAQController.cs (Controlador principal)
│       ├── Interfaces/ (Contratos)
│       ├── Managers/ (DeviceManager, ChannelManager, ProfileManager)
│       ├── Services/ (SignalGenerator, ConsoleLogger, SignalLUT)
│       ├── Models/ (ChannelState, DeviceInfo, DeviceType)
│       ├── Exceptions/ (DAQException, etc.)
│       └── Profiles/ (XML configs)
└── UI/
    ├── ConsoleUI.cs (UI principal actual)
    ├── Interfaces/ (IConsoleService, IDeviceMenuHandler)
    ├── Services/ (ConsoleService, DeviceDetectionService)
    ├── Managers/ (MenuManager, AnalogMenuManager, DigitalMenuManager)
    └── Models/ (DAQDevice)
\\\

### 2.2 Fortalezas Identificadas

✅ **Separación Core/UI perfecta**
- Core no conoce UI
- Facilita cambio de interfaz

✅ **Patrón Strategy en menús**
- IDeviceMenuHandler permite diferentes implementaciones
- Dictionary en MenuManager para dispatch por DeviceType

✅ **Inyección de dependencias**
- DAQController acepta implementaciones custom

✅ **Abstracción de servicios**
- IConsoleService puede extenderse a IUIService

### 2.3 Capacidades de Hardware

#### PCIE-1824 (Analógica)
- **Canales**: 32 AO (Analog Output)
- **Capacidad**: Solo ESCRITURA
- **Resolución**: 16 bits
- **Rango**: Configurable (±10V típico)
- **Visualización**: Tracking de comandos enviados

#### PCI-1735U (Digital)
- **Canales**: 32 DI/DO configurables (4 puertos × 8 bits)
- **Capacidad**: LECTURA y ESCRITURA
- **Contadores**: 3 contadores programables
- **Visualización**: Lectura real en tiempo real

---

## 3. Requisitos del Sistema

### 3.1 Requisitos Funcionales

#### RF-01: Visualización en Tiempo Real
- La aplicación DEBE mostrar gráficos en tiempo real de:
  - Comandos enviados a salidas analógicas (PCIE-1824)
  - Lecturas de entradas digitales (PCI-1735U)
  
#### RF-02: Control de Dispositivos
- La aplicación DEBE permitir:
  - Seleccionar entre dispositivos detectados
  - Establecer valores DC en canales analógicos
  - Generar rampas de voltaje
  - Generar señales (seno, cuadrada, etc.)
  - Leer/escribir puertos digitales

#### RF-03: Detección Automática
- La aplicación DEBE detectar automáticamente dispositivos conectados
- DEBE mostrar información de cada dispositivo (BoardId, tipo, canales)

#### RF-04: Monitoreo Simultáneo
- La aplicación DEBE permitir monitorear ambas tarjetas simultáneamente
- DEBE actualizar gráficos a frecuencia configurable (10-100 Hz)

#### RF-05: Retrocompatibilidad
- La aplicación DEBE mantener modo consola como opción (flag -console)

### 3.2 Requisitos No Funcionales

#### RNF-01: Rendimiento
- Actualización de gráficos: Mínimo 20 Hz (50ms)
- Latencia de comando: < 10ms
- Buffer de visualización: 1000 puntos mínimo

#### RNF-02: Usabilidad
- Interfaz intuitiva y clara
- Controles accesibles con mouse y teclado
- Feedback visual de estado de dispositivos

#### RNF-03: Mantenibilidad
- Código modular y bien documentado
- Separación clara de responsabilidades
- Tests unitarios para componentes críticos

---

## 4. Estrategia de Implementación

### 4.1 Principios Fundamentales

1. **No Romper Nada**: Mantener código existente funcional
2. **Desarrollo Paralelo**: Crear estructura WPF sin modificar consola
3. **Integración Gradual**: Conectar por fases, validando cada etapa
4. **Reutilización**: Aprovechar Core existente al máximo

### 4.2 Enfoque de Desarrollo

- **Patrón MVVM**: Separar lógica de presentación
- **Event-Driven**: Usar eventos para actualización de UI
- **Async/Await**: Operaciones no bloqueantes
- **SOLID principles**: Mantener arquitectura limpia

---

## 5. Estructura de Archivos Propuesta

### 5.1 Nueva Estructura UI

\\\
UI/
├── ConsoleUI.cs [MANTENER]
├── WPF/ [NUEVA CARPETA]
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── Windows/
│   │   ├── MainWindow.xaml
│   │   └── MainWindow.xaml.cs
│   ├── Views/
│   │   ├── AnalogControlPanel.xaml
│   │   ├── AnalogControlPanel.xaml.cs
│   │   ├── DigitalControlPanel.xaml
│   │   ├── DigitalControlPanel.xaml.cs
│   │   ├── RealtimeChartControl.xaml
│   │   └── RealtimeChartControl.xaml.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── AnalogControlViewModel.cs
│   │   └── DigitalMonitorViewModel.cs
│   └── Converters/
│       └── DeviceTypeToVisibilityConverter.cs
├── Services/ [AMPLIAR]
│   ├── ConsoleService.cs [MANTENER]
│   ├── AnalogOutputTracker.cs [NUEVO]
│   ├── DigitalInputMonitor.cs [NUEVO]
│   └── RealtimeDataService.cs [NUEVO]
└── [... resto sin cambios ...]
\\\

### 5.2 Archivos a Crear (Total: 17 archivos nuevos)

#### WPF Core (3 archivos)
1. \UI/WPF/App.xaml\
2. \UI/WPF/App.xaml.cs\
3. \UI/WPF/Windows/MainWindow.xaml\

#### Views (6 archivos)
4. \UI/WPF/Windows/MainWindow.xaml.cs\
5. \UI/WPF/Views/AnalogControlPanel.xaml\
6. \UI/WPF/Views/AnalogControlPanel.xaml.cs\
7. \UI/WPF/Views/DigitalControlPanel.xaml\
8. \UI/WPF/Views/DigitalControlPanel.xaml.cs\
9. \UI/WPF/Views/RealtimeChartControl.xaml\
10. \UI/WPF/Views/RealtimeChartControl.xaml.cs\

#### ViewModels (3 archivos)
11. \UI/WPF/ViewModels/MainViewModel.cs\
12. \UI/WPF/ViewModels/AnalogControlViewModel.cs\
13. \UI/WPF/ViewModels/DigitalMonitorViewModel.cs\

#### Services (3 archivos)
14. \UI/Services/AnalogOutputTracker.cs\
15. \UI/Services/DigitalInputMonitor.cs\
16. \UI/Services/RealtimeDataService.cs\

#### Converters (1 archivo)
17. \UI/WPF/Converters/DeviceTypeToVisibilityConverter.cs\

### 5.3 Archivos a Modificar (3 archivos)

1. \Program.cs\ - Agregar lógica para WPF vs Consola
2. \LAMP_DAQ_Control_v0.8.csproj\ - Referencias WPF y NuGet
3. \App.config\ - Configuraciones adicionales si necesario

---

## 6. Plan de Desarrollo por Fases

### FASE 0: Preparación (1 hora)
**Estado**: ⏳ Pendiente

#### Tareas:
- [x] Crear documento de plan (este archivo)
- [ ] Crear carpetas de estructura WPF
- [ ] Actualizar .csproj con referencias WPF
- [ ] Instalar paquete ScottPlot.WPF via NuGet

#### Comandos:
\\\powershell
# Crear estructura de carpetas
New-Item -ItemType Directory -Path \"UI\\WPF\\Windows\"
New-Item -ItemType Directory -Path \"UI\\WPF\\Views\"
New-Item -ItemType Directory -Path \"UI\\WPF\\ViewModels\"
New-Item -ItemType Directory -Path \"UI\\WPF\\Converters\"
\\\

#### Validación:
- [ ] Proyecto compila sin errores
- [ ] Estructura de carpetas creada

---

### FASE 1: Servicios de Monitoreo (3-4 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: FASE 0

#### 1.1 AnalogOutputTracker
**Ubicación**: \UI/Services/AnalogOutputTracker.cs\

**Responsabilidades**:
- Registrar comandos enviados a salidas analógicas
- Mantener buffer circular de últimos N puntos
- Emitir eventos cuando se escriben valores
- Proveer datos históricos para gráficos

**Interfaz Pública**:
\\\csharp
public class AnalogOutputTracker
{
    public event EventHandler<AnalogDataEventArgs> DataRecorded;
    public void RecordWrite(int channel, double voltage, DateTime timestamp);
    public DataPoint[] GetChannelHistory(int channel);
    public void ClearHistory(int channel);
}
\\\

#### 1.2 DigitalInputMonitor
**Ubicación**: \UI/Services/DigitalInputMonitor.cs\

**Responsabilidades**:
- Leer entradas digitales periódicamente
- Emitir eventos con datos leídos
- Manejar errores de lectura
- Proveer estado actual de puertos

**Interfaz Pública**:
\\\csharp
public class DigitalInputMonitor : IDisposable
{
    public event EventHandler<DigitalDataEventArgs> DataReceived;
    public void StartMonitoring(int deviceNumber, int intervalMs);
    public void StopMonitoring();
    public byte[] GetCurrentState();
}
\\\

#### Validación:
- [ ] AnalogOutputTracker registra correctamente
- [ ] DigitalInputMonitor lee datos reales
- [ ] Eventos se disparan correctamente
- [ ] Tests unitarios pasan

---

### FASE 2: Entry Point WPF (2 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: FASE 0

#### Tareas:
- [ ] Modificar Program.cs para soportar WPF
- [ ] Crear App.xaml y App.xaml.cs
- [ ] Implementar manejo de excepciones global

#### Modificación Program.cs:
\\\csharp
[STAThread]
static void Main(string[] args)
{
    bool useConsole = args.Length > 0 && args[0] == \"-console\";
    
    if (useConsole)
        RunConsoleMode().Wait();
    else
        RunWPFMode();
}
\\\

#### Validación:
- [ ] Aplicación inicia en modo WPF
- [ ] Aplicación inicia en modo consola con flag
- [ ] Excepciones se capturan correctamente

---

### FASE 3: Ventana Principal Base (3 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: FASE 2

#### Tareas:
- [ ] Crear MainWindow.xaml con layout base
- [ ] Crear MainViewModel.cs
- [ ] Implementar detección de dispositivos en UI
- [ ] Mostrar lista de dispositivos

#### Layout Principal:
\\\
┌─────────────────────────────────────┐
│ LAMP DAQ Control v0.8         [_][□][×]│
├─────────────────────────────────────┤
│ ┌─ Dispositivos Detectados ───────┐│
│ │ ☑ PCIE-1824 (AO) BID#12         ││
│ │ ☑ PCI-1735U (DI/DO) BID#3       ││
│ └─────────────────────────────────┘│
│ [Área de contenido dinámico]       │
└─────────────────────────────────────┘
\\\

#### Validación:
- [ ] Ventana se muestra correctamente
- [ ] Dispositivos se detectan y listan
- [ ] Binding de ViewModel funciona

---

### FASE 4: Panel de Control Analógico (4 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: FASE 1, FASE 3

#### Tareas:
- [ ] Crear AnalogControlPanel.xaml
- [ ] Crear AnalogControlViewModel.cs
- [ ] Implementar controles (canal, voltaje, botones)
- [ ] Integrar con DAQController
- [ ] Agregar gráfico ScottPlot

#### Componentes:
- NumericUpDown para canal (0-31)
- Slider + TextBox para voltaje (0-10V)
- Botones: [Establecer DC] [Generar Rampa] [Generar Señal]
- ScottPlot WpfPlot para visualización

#### Validación:
- [ ] Controles responden correctamente
- [ ] Comandos se envían a tarjeta
- [ ] Gráfico muestra comandos enviados
- [ ] Actualización en tiempo real funciona

---

### FASE 5: Panel de Monitoreo Digital (4 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: FASE 1, FASE 3

#### Tareas:
- [ ] Crear DigitalControlPanel.xaml
- [ ] Crear DigitalMonitorViewModel.cs  
- [ ] Implementar grid de bits (32 indicadores)
- [ ] Integrar DigitalInputMonitor
- [ ] Agregar gráfico digital ScottPlot

#### Componentes:
- Grid 4×8 con CheckBox/LED para cada bit
- Labels para puertos (P0, P1, P2, P3)
- ScottPlot para timeline digital
- Control de frecuencia de lectura

#### Validación:
- [ ] LEDs/CheckBoxes reflejan estado real
- [ ] Lectura en tiempo real funciona
- [ ] Gráfico digital se actualiza
- [ ] Frecuencia configurable funciona

---

### FASE 6: Gráficos en Tiempo Real (3 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: FASE 4, FASE 5

#### Tareas:
- [ ] Optimizar actualización de ScottPlot
- [ ] Implementar auto-scroll en gráficos
- [ ] Agregar controles de zoom/pan
- [ ] Implementar leyendas y tooltips
- [ ] Configurar colores por canal

#### Optimizaciones ScottPlot:
\\\csharp
// Usar SignalPlot para mejor rendimiento
wpfPlot.Plot.AddSignal(data, sampleRate);
wpfPlot.Plot.AxisAuto();
wpfPlot.Refresh();
\\\

#### Validación:
- [ ] Gráficos se actualizan fluidamente (>20 FPS)
- [ ] No hay lags ni freezes
- [ ] Memoria no crece indefinidamente
- [ ] CPU usage < 30%

---

### FASE 7: Integración y Pulido (2 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: Todas las fases anteriores

#### Tareas:
- [ ] Conectar todos los componentes
- [ ] Agregar manejo de errores en UI
- [ ] Implementar mensajes de estado
- [ ] Agregar about/help dialog
- [ ] Crear iconos y recursos visuales

#### Validación:
- [ ] Aplicación funciona end-to-end
- [ ] Errores se manejan gracefully
- [ ] UX es fluida e intuitiva

---

### FASE 8: Testing y Documentación (2 horas)
**Estado**: ⏳ Pendiente  
**Dependencias**: FASE 7

#### Tareas:
- [ ] Pruebas con hardware real
- [ ] Pruebas de estrés (lecturas rápidas)
- [ ] Actualizar README.md
- [ ] Crear guía de usuario
- [ ] Documentar API pública

#### Validación:
- [ ] Todas las funciones verificadas con hardware
- [ ] Sin memory leaks después de 1 hora
- [ ] Documentación completa

---

## 7. Puntos de Integración

### 7.1 Con DAQController
\\\csharp
// En ViewModel
private readonly DAQController _controller;

public void SetVoltage(int channel, double voltage)
{
    _controller.WriteVoltage(channel, voltage);
    _analogTracker.RecordWrite(channel, voltage, DateTime.Now);
}
\\\

### 7.2 Con DeviceDetectionService
\\\csharp
// En MainViewModel
var detectionService = new DeviceDetectionService(new WpfUIService());
var devices = detectionService.DetectDAQDevices();
Devices = new ObservableCollection<DAQDevice>(devices);
\\\

### 7.3 Con SignalGenerator
\\\csharp
// Suscribirse a eventos existentes
_controller.SignalGenerator.OnSampleGenerated += (s, sample) =>
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        AddPointToChart(sample);
    });
};
\\\

---

## 8. Riesgos y Mitigaciones

### Riesgo 1: Rendimiento de Gráficos
**Probabilidad**: Media  
**Impacto**: Alto

**Mitigación**:
- Usar ScottPlot que está optimizado para datos científicos
- Limitar buffer a 1000 puntos
- Usar SignalPlot en lugar de ScatterPlot
- Actualizar a frecuencia razonable (20-50 Hz)

### Riesgo 2: Threading Issues
**Probabilidad**: Alta  
**Impacto**: Alto

**Mitigación**:
- Siempre usar Dispatcher.Invoke para actualizar UI
- Usar async/await correctamente
- Implementar CancellationToken para tareas largas

### Riesgo 3: Memory Leaks
**Probabilidad**: Media  
**Impacto**: Medio

**Mitigación**:
- Implementar IDisposable correctamente
- Unsubscribir eventos cuando sea necesario
- Usar WeakEventManager para eventos WPF
- Profiling con Visual Studio Memory Profiler

### Riesgo 4: Incompatibilidad con Hardware
**Probabilidad**: Baja  
**Impacto**: Alto

**Mitigación**:
- Reutilizar código Core probado
- No modificar lógica de comunicación con hardware
- Testing exhaustivo con dispositivos reales

---

## 9. Criterios de Aceptación

### CA-01: Funcionalidad Completa
- [ ] Todos los controles de consola disponibles en WPF
- [ ] Ambas tarjetas funcionan simultáneamente
- [ ] Gráficos muestran datos correctos

### CA-02: Rendimiento
- [ ] Actualización de gráficos ≥ 20 Hz
- [ ] Latencia de comandos < 10ms
- [ ] CPU usage < 30% en operación normal
- [ ] Sin memory leaks en 2 horas de operación

### CA-03: Usabilidad
- [ ] Interfaz intuitiva (usuario puede usar sin manual)
- [ ] Feedback visual claro de todas las acciones
- [ ] Manejo de errores con mensajes comprensibles

### CA-04: Mantenibilidad
- [ ] Código documentado con XML comments
- [ ] Arquitectura MVVM correctamente implementada
- [ ] Separación clara de responsabilidades
- [ ] README actualizado con instrucciones

### CA-05: Compatibilidad
- [ ] Modo consola sigue funcionando
- [ ] Funciona en Windows 10/11
- [ ] No requiere dependencias adicionales del usuario

---

## 10. Cronograma

| Fase | Duración | Inicio | Fin |
|------|----------|--------|-----|
| FASE 0 | 1h | - | - |
| FASE 1 | 4h | - | - |
| FASE 2 | 2h | - | - |
| FASE 3 | 3h | - | - |
| FASE 4 | 4h | - | - |
| FASE 5 | 4h | - | - |
| FASE 6 | 3h | - | - |
| FASE 7 | 2h | - | - |
| FASE 8 | 2h | - | - |
| **TOTAL** | **25h** (~3-4 días) | - | - |

---

## 11. Recursos y Referencias

### Documentación
- [WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [ScottPlot Documentation](https://scottplot.net/cookbook/4.1/)
- [MVVM Pattern](https://docs.microsoft.com/en-us/xamarin/xamarin-forms/enterprise-application-patterns/mvvm)
- Advantech SDK PDFs en \Core/Docs/\

### Herramientas
- Visual Studio 2019/2022
- NuGet Package Manager
- Git para control de versiones

---

## 12. Notas de Implementación

### Convenciones de Código
- **Namespaces**: \LAMP_DAQ_Control_v0_8.UI.WPF.*\
- **ViewModels**: Sufijo \ViewModel\
- **Events**: Prefijo \On\
- **Async methods**: Sufijo \Async\

### Best Practices
- Usar \INotifyPropertyChanged\ en ViewModels
- Implementar \ICommand\ para acciones de botones
- Usar \ObservableCollection<T>\ para listas dinámicas
- Validar inputs antes de enviar a Core

---

## 13. Aprobaciones

| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Desarrollador | - | - | 17/11/2025 |
| Revisor | - | - | - |
| Aprobador | - | - | - |

---

**Fin del Documento**
