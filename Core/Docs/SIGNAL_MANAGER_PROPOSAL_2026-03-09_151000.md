# PROPUESTA: GESTIONADOR DE SEÑALES I/O
## Sistema de Secuenciación y Visualización Temporal para Experimentos de Física Óptica

**Fecha:** 2026-03-09 15:10:00  
**Proyecto:** LAMP DAQ Control v0.8  
**Autor:** Cascade AI Assistant  
**Estado:** Propuesta para Aprobación

---

## 📋 RESUMEN EJECUTIVO

### Objetivo Principal
Desarrollar un **Gestionador de Señales de Entrada y Salida** (Signal Manager) que permita:
- Control programático de señales digitales y analógicas
- Visualización en diagrama temporal con potencia, frecuencia y modos
- Secuenciación tipo "LEGO" (plug & play) para experimentos de física óptica
- Reutilización de funciones existentes del panel de prueba actual

### Problema a Resolver
Los experimentos de física óptica requieren:
1. Secuencias de control precisas y repetibles
2. Sincronización entre múltiples dispositivos
3. Visualización clara del estado temporal de señales
4. Capacidad de crear secuencias complejas de forma modular

### Solución Propuesta
Sistema integrado en LAMP DAQ que combine:
- **Sequencer Engine:** Motor de secuenciación temporal
- **Timeline Visualizer:** Diagrama temporal interactivo
- **Signal Library:** Biblioteca de señales reutilizables
- **Experiment Templates:** Plantillas para experimentos tipo

---

## 🔬 INVESTIGACIÓN: ESTADO DEL ARTE

### 1. Sistemas Académicos de Control Temporal

#### **Prawnblaster/PrawnDO** (ArXiv 2024)
**Fuente:** "Experimental timing and control using microcontrollers" - ArXiv:2406.17603v1

**Características Clave:**
- Basado en microcontroladores Raspberry Pi Pico (RP2040)
- Dos tipos de generación de pulsos:
  - **Pseudoclocks:** Pulsos repetitivos con periodo variable
  - **Arbitrary Digital Pulses:** Control completo de timing
- Resolución temporal: 10ns
- Escalable mediante topología distribuida

**Ventajas:**
- ✅ Bajo costo (~$5 USD por board)
- ✅ Alta precisión temporal
- ✅ Programación simple (C/C++)
- ✅ Escalabilidad mediante cascada

**Desventajas:**
- ❌ Requiere clock común para sincronización
- ❌ Múltiples conexiones USB
- ❌ Nivel lógico LVCMOS 3.3V (requiere buffers)

**Aplicabilidad al Proyecto:**
- Concepto de pseudoclock es útil para señales repetitivas
- Arquitectura distribuida inspira diseño modular
- Run-length encoding para especificar pulsos

---

#### **openDAQ** (Open Source SDK)
**Fuente:** https://github.com/openDAQ/openDAQ

**Características Clave:**
- SDK open-source para integración de dispositivos DAQ
- API genérica común para múltiples fabricantes
- Framework de procesamiento de señales
- Soporte OPC UA y WebSocket
- Wrappers para Python, C++, Delphi

**Arquitectura:**
```
Control Application (Client)
    ↓ OPC UA / WebSocket
DAQ Device (Server)
    ↓ Signal Processing Blocks
Physical Hardware
```

**Ventajas:**
- ✅ Abstracción de hardware
- ✅ Procesamiento de señal modular
- ✅ Multi-lenguaje (Python, C++)
- ✅ Standards-based (OPC UA, MQTT)

**Desventajas:**
- ❌ Overhead de comunicación
- ❌ Requiere adaptar drivers existentes
- ❌ Complejidad de setup inicial

**Aplicabilidad al Proyecto:**
- Concepto de "Signal Processing Blocks" modulares
- API genérica inspiración para abstracción
- No implementar completo (muy pesado), adaptar conceptos

---

#### **LabVIEW Waveform Sequencer** (NI)
**Fuente:** NI RF-VST Documentation

**Características Clave:**
- Generación de waveforms arbitrarias
- Linking y looping de waveforms (sequencing)
- Generación de markers
- Advanced triggering modes

**Funcionalidades:**
- **Waveform Editor:** Edición gráfica de formas de onda
- **Sequence Builder:** Construcción de secuencias
- **Timing Engine:** Motor de timing preciso
- **Script Language:** Scripting para secuencias complejas

**Ventajas:**
- ✅ Interfaz gráfica intuitiva
- ✅ Editor visual de waveforms
- ✅ Biblioteca extensa de señales
- ✅ Hardware-timed generation

**Desventajas:**
- ❌ Propietario y costoso
- ❌ Vendor lock-in
- ❌ No open source

**Aplicabilidad al Proyecto:**
- **INSPIRACIÓN PRINCIPAL** para UI/UX
- Editor visual de timeline
- Concepto de "waveform library"
- Scripting para automatización

---

#### **Python Instrument Control** (Bluesky, PyMeasure)
**Fuente:** Research papers + GitHub

**Características Clave:**
- **Bluesky:** Framework de Brookhaven National Lab
- **PyMeasure:** Instrument control library
- **QCoDeS:** Quantum measurement framework

**Arquitectura Bluesky:**
```python
# Experiment Plan (declarative)
def scan_plan():
    yield from move(motor, 0)
    yield from trigger_and_read([detector])
    yield from move(motor, 10)
    
# Run Engine executes plan
RE(scan_plan())
```

**Ventajas:**
- ✅ Declarativo y reproducible
- ✅ Metadata automático
- ✅ Separación plan/ejecución
- ✅ Gran comunidad científica

**Desventajas:**
- ❌ Python (más lento que C#)
- ❌ Curva de aprendizaje
- ❌ Overhead de framework

**Aplicabilidad al Proyecto:**
- Concepto de "plan" declarativo
- Separación entre definición y ejecución
- Metadata y logging automático

---

### 2. Análisis Comparativo

| Sistema | Resolución | Escalabilidad | Open Source | Aplicable |
|---------|-----------|---------------|-------------|-----------|
| Prawnblaster | 10ns | Alta (distribuido) | ✅ Sí | Conceptos |
| openDAQ | Variable | Media | ✅ Sí | Arquitectura |
| LabVIEW | Hardware-dependent | Alta | ❌ No | **UI/UX** |
| Python (Bluesky) | Software | Media | ✅ Sí | Workflow |

**Conclusión de Investigación:**
- LabVIEW provee el mejor modelo de UI para visualización temporal
- Prawnblaster inspira arquitectura de timing preciso
- Python frameworks muestran importancia de declarativo
- openDAQ demuestra valor de modularidad

---

## 🏗️ ARQUITECTURA PROPUESTA

### Visión General

```
┌─────────────────────────────────────────────────────────────┐
│                    LAMP DAQ Control v0.8                     │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │          Signal Manager Module (NUEVO)             │    │
│  │                                                     │    │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────┐ │    │
│  │  │   Sequence   │  │   Timeline   │  │  Signal  │ │    │
│  │  │    Engine    │  │  Visualizer  │  │ Library  │ │    │
│  │  └──────────────┘  └──────────────┘  └──────────┘ │    │
│  │                                                     │    │
│  │  ┌──────────────────────────────────────────────┐  │    │
│  │  │        Execution Engine                      │  │    │
│  │  └──────────────────────────────────────────────┘  │    │
│  └────────────────────────────────────────────────────┘    │
│                           ↓                                 │
│  ┌────────────────────────────────────────────────────┐    │
│  │         DAQController (EXISTENTE)                   │    │
│  │  DeviceManager | SignalGenerator | ChannelManager  │    │
│  └────────────────────────────────────────────────────┘    │
│                           ↓                                 │
│  ┌────────────────────────────────────────────────────┐    │
│  │              Hardware Layer                         │    │
│  │      PCIe-1824 (Analog) | PCI-1735U (Digital)      │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

### Componentes Principales

#### 1. **Sequence Engine** (Motor de Secuenciación)

**Responsabilidades:**
- Definir y almacenar secuencias de señales
- Validar consistencia temporal
- Gestionar dependencias entre señales
- Calcular timing absoluto de eventos

**Clases Principales:**
```csharp
// Core/SignalManager/Models/SignalSequence.cs
public class SignalSequence
{
    public string Name { get; set; }
    public string Description { get; set; }
    public List<SignalEvent> Events { get; set; }
    public TimeSpan TotalDuration { get; }
    public Dictionary<string, object> Metadata { get; set; }
}

// Core/SignalManager/Models/SignalEvent.cs
public class SignalEvent
{
    public string EventId { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public int Channel { get; set; }
    public DeviceType DeviceType { get; set; }
    public SignalEventType Type { get; set; } // DC, Ramp, Waveform, Digital
    public Dictionary<string, double> Parameters { get; set; }
}

// Core/SignalManager/Services/SequenceEngine.cs
public class SequenceEngine
{
    public void CreateSequence(string name);
    public void AddEvent(string sequenceId, SignalEvent evt);
    public void ValidateSequence(string sequenceId);
    public SignalSequence GetSequence(string sequenceId);
    public void SaveSequence(string sequenceId, string filePath);
    public SignalSequence LoadSequence(string filePath);
}
```

**Formato de Archivo (JSON):**
```json
{
  "name": "Laser_Alignment_Sequence",
  "description": "Secuencia para alineación de láser",
  "version": "1.0",
  "created": "2026-03-09T15:10:00Z",
  "totalDuration": "00:00:05.000",
  "events": [
    {
      "eventId": "evt_001",
      "startTime": "00:00:00.000",
      "duration": "00:00:01.000",
      "channel": 0,
      "deviceType": "Analog",
      "type": "Ramp",
      "parameters": {
        "startVoltage": 0.0,
        "endVoltage": 5.0
      }
    },
    {
      "eventId": "evt_002",
      "startTime": "00:00:01.000",
      "duration": "00:00:02.000",
      "channel": 0,
      "deviceType": "Analog",
      "type": "DC",
      "parameters": {
        "voltage": 5.0
      }
    }
  ]
}
```

---

#### 2. **Timeline Visualizer** (Visualizador Temporal)

**Responsabilidades:**
- Renderizar diagrama temporal de señales
- Mostrar múltiples canales simultáneamente
- Interacción: zoom, pan, selección
- Indicadores de estado: activo, pausa, error

**Tecnología UI:**
- **WPF Canvas** para renderizado 2D
- **Custom Controls** para timeline
- **LiveCharts** para gráficos de señales

**Componentes Visuales:**
```
┌────────────────────────────────────────────────────────┐
│  Timeline Controls  [▶ Play] [⏸ Pause] [⏹ Stop]      │
├────────────────────────────────────────────────────────┤
│                                                         │
│  Ch0 (A) │████████████░░░░░░░░░░░░░░░░░░░░░░░░░       │
│          │ Ramp 5V        DC 5V                        │
│          │                                             │
│  Ch1 (A) │░░░░░░░░░░░░░░████████████░░░░░░░░░░        │
│          │            Sine 100Hz                       │
│          │                                             │
│  Ch0 (D) │░░░░░░░░░░██░░░░░░░░░░██░░░░░░░░░░          │
│          │        ON         ON                        │
│          │                                             │
│          └─────┬─────┬─────┬─────┬─────┬─────>       │
│              0s    1s    2s    3s    4s    5s          │
│                                                         │
│  ┌───────────────────────────────────────────────┐    │
│  │ Event Details                                  │    │
│  │ Selected: evt_001                              │    │
│  │ Type: Ramp | Ch: 0 | Start: 0s | Duration: 1s │    │
│  │ Parameters: 0V → 5V                            │    │
│  └───────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────────┘
```

**WPF XAML Structure:**
```xml
<UserControl x:Class="UI.WPF.Views.TimelineVisualizerView">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <!-- Controls -->
            <RowDefinition Height="*"/>    <!-- Timeline -->
            <RowDefinition Height="Auto"/> <!-- Details -->
        </Grid.RowDefinitions>
        
        <!-- Playback Controls -->
        <StackPanel Grid.Row="0" Orientation="Horizontal">
            <Button Command="{Binding PlayCommand}">▶ Play</Button>
            <Button Command="{Binding PauseCommand}">⏸ Pause</Button>
            <Button Command="{Binding StopCommand}">⏹ Stop</Button>
            <Slider Value="{Binding CurrentTime}" Maximum="{Binding TotalDuration}"/>
        </StackPanel>
        
        <!-- Timeline Canvas -->
        <ScrollViewer Grid.Row="1">
            <Canvas x:Name="TimelineCanvas" 
                    Background="White"
                    MouseWheel="OnMouseWheel"
                    MouseDown="OnMouseDown"/>
        </ScrollViewer>
        
        <!-- Event Details -->
        <Border Grid.Row="2" BorderBrush="Gray" BorderThickness="1">
            <StackPanel Margin="10">
                <TextBlock Text="{Binding SelectedEvent.Description}"/>
                <TextBlock Text="{Binding SelectedEvent.Parameters}"/>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

---

#### 3. **Signal Library** (Biblioteca de Señales)

**Responsabilidades:**
- Almacenar señales predefinidas reutilizables
- Categorización (DC, Ramp, Waveform, Digital Pulse)
- Plantillas parametrizadas
- Import/Export de señales

**Categorías:**
1. **DC Signals:** Voltaje constante
2. **Ramp Signals:** Rampas lineales/exponenciales
3. **Waveform Signals:** Sine, Square, Triangle, Custom
4. **Digital Pulses:** ON/OFF, PWM, Pulse Train
5. **Composite Signals:** Combinaciones complejas

**Ejemplo de Biblioteca:**
```json
{
  "library": "LAMP_Signal_Library_v1",
  "signals": [
    {
      "id": "sig_laser_warmup",
      "name": "Laser Warmup",
      "category": "Ramp",
      "description": "Ramp from 0V to 5V in 2s for laser warmup",
      "parameters": {
        "startVoltage": 0.0,
        "endVoltage": 5.0,
        "duration": 2.0,
        "rampType": "Linear"
      }
    },
    {
      "id": "sig_modulation_100hz",
      "name": "100Hz Modulation",
      "category": "Waveform",
      "description": "Sine wave 100Hz, 5V amplitude, 5V offset",
      "parameters": {
        "frequency": 100,
        "amplitude": 5.0,
        "offset": 5.0,
        "waveformType": "Sine"
      }
    },
    {
      "id": "sig_shutter_pulse",
      "name": "Shutter Pulse",
      "category": "Digital",
      "description": "100ms ON pulse for shutter control",
      "parameters": {
        "state": "High",
        "duration": 0.1
      }
    }
  ]
}
```

---

#### 4. **Execution Engine** (Motor de Ejecución)

**Responsabilidades:**
- Ejecutar secuencias en tiempo real
- Sincronización multi-canal
- Manejo de errores durante ejecución
- Logging de eventos ejecutados

**Algoritmo de Ejecución:**
```csharp
public class ExecutionEngine
{
    private DAQController _daqController;
    private SignalSequence _currentSequence;
    private Stopwatch _executionTimer;
    private CancellationTokenSource _cts;
    
    public async Task ExecuteSequenceAsync(SignalSequence sequence)
    {
        _currentSequence = sequence;
        _executionTimer = Stopwatch.StartNew();
        _cts = new CancellationTokenSource();
        
        // Sort events by start time
        var sortedEvents = sequence.Events.OrderBy(e => e.StartTime).ToList();
        
        // Execute events
        foreach (var evt in sortedEvents)
        {
            // Wait until event start time
            await WaitUntilAsync(evt.StartTime, _cts.Token);
            
            // Execute event
            await ExecuteEventAsync(evt);
            
            // Log execution
            Logger.Info($"Event {evt.EventId} executed at {_executionTimer.Elapsed}");
        }
    }
    
    private async Task WaitUntilAsync(TimeSpan targetTime, CancellationToken ct)
    {
        while (_executionTimer.Elapsed < targetTime && !ct.IsCancellationRequested)
        {
            await Task.Delay(1, ct); // 1ms precision
        }
    }
    
    private async Task ExecuteEventAsync(SignalEvent evt)
    {
        switch (evt.Type)
        {
            case SignalEventType.DC:
                _daqController.SetChannelValue(evt.Channel, evt.Parameters["voltage"]);
                break;
            case SignalEventType.Ramp:
                await _daqController.RampChannelValue(
                    evt.Channel, 
                    evt.Parameters["endVoltage"], 
                    (int)evt.Duration.TotalMilliseconds);
                break;
            case SignalEventType.Waveform:
                _daqController.StartSignalGeneration(
                    evt.Channel,
                    evt.Parameters["frequency"],
                    evt.Parameters["amplitude"],
                    evt.Parameters["offset"]);
                break;
            // ... otros tipos
        }
    }
}
```

---

### Interfaz de Usuario Propuesta

#### **Ventana Principal del Signal Manager**

```
┌──────────────────────────────────────────────────────────────┐
│  Signal Manager - LAMP DAQ Control                           │
├──────────────────────────────────────────────────────────────┤
│  [File] [Edit] [View] [Sequence] [Help]                     │
├─────────────────┬────────────────────────────────────────────┤
│                 │                                            │
│  Sequences      │         Timeline View                      │
│  ─────────      │  ┌──────────────────────────────────┐     │
│  ▶ Laser Align  │  │ [▶] [⏸] [⏹]  Time: 0.00s        │     │
│    Modulation   │  ├──────────────────────────────────┤     │
│    Pulse Train  │  │ Ch0(A) ████░░░░░░░░░░░░░░        │     │
│                 │  │ Ch1(A) ░░░░████████░░░░░░        │     │
│  + New Sequence │  │ P0(D)  ░░██░░░░░░██░░░░░░        │     │
│                 │  └──────────────────────────────────┘     │
│  Signal Library │                                            │
│  ─────────────  │         Event Details                      │
│  📁 DC Signals  │  ┌──────────────────────────────────┐     │
│  📁 Ramps       │  │ Event: evt_001                    │     │
│  📁 Waveforms   │  │ Type: Ramp                        │     │
│  📁 Digital     │  │ Channel: 0 (Analog)               │     │
│                 │  │ Start: 0.000s | Duration: 1.000s  │     │
│  🔧 Create New  │  │ Parameters:                       │     │
│                 │  │   Start: 0.0V  End: 5.0V          │     │
│                 │  │ [Edit] [Delete] [Duplicate]       │     │
│                 │  └──────────────────────────────────┘     │
└─────────────────┴────────────────────────────────────────────┘

---

## 📅 PLANIFICACIÓN: CARTA GANTT

### Resumen de Fases

| Fase | Nombre | Duración | Esfuerzo | Prioridad |
|------|--------|----------|----------|-----------|
| 0 | Setup y Preparación | 1 semana | 40h | CRÍTICA |
| 1 | Core: Sequence Engine | 2 semanas | 80h | CRÍTICA |
| 2 | UI: Timeline Visualizer | 3 semanas | 120h | ALTA |
| 3 | Signal Library | 1.5 semanas | 60h | ALTA |
| 4 | Execution Engine | 2 semanas | 80h | CRÍTICA |
| 5 | Integration & Testing | 2 semanas | 80h | CRÍTICA |
| 6 | Documentation & Polish | 1 semana | 40h | MEDIA |
| **TOTAL** | | **12.5 semanas** | **500h** | |

### Carta Gantt Detallada

```
FASE 0: SETUP Y PREPARACIÓN (Semana 1)
════════════════════════════════════════════════════════════════
│████████│ Semana 1
└─────────────────────────────────────────────────────────────→

Tareas:
✓ Revisar y aprobar propuesta [2d]
✓ Setup de estructura de proyecto [1d]
  - Crear carpetas: Core/SignalManager/{Models, Services, ViewModels}
  - Crear carpetas: UI/WPF/Views/SignalManager
✓ Definir interfaces principales [2d]
  - ISequenceEngine
  - ISignalLibrary
  - IExecutionEngine

Entregables:
- Estructura de carpetas creada
- Interfaces base definidas
- Plan de trabajo aprobado

────────────────────────────────────────────────────────────────

FASE 1: CORE - SEQUENCE ENGINE (Semanas 2-3)
════════════════════════════════════════════════════════════════
│░░░░░░░░│████████████████│ Semanas 2-3
└─────────────────────────────────────────────────────────────→

Tareas:
✓ Implementar modelos de datos [3d]
  - SignalSequence.cs
  - SignalEvent.cs
  - SignalEventType enum
  - DeviceType enum

✓ Implementar SequenceEngine.cs [4d]
  - CreateSequence()
  - AddEvent()
  - RemoveEvent()
  - ValidateSequence()
  - GetSequence()

✓ Serialización JSON [2d]
  - SaveSequence() → JSON
  - LoadSequence() ← JSON
  - Schema validation

✓ Tests unitarios [1d]
  - SequenceEngineTests.cs
  - Cobertura >80%

Entregables:
- SequenceEngine funcional
- JSON import/export working
- 15+ tests unitarios pasando

────────────────────────────────────────────────────────────────

FASE 2: UI - TIMELINE VISUALIZER (Semanas 4-6)
════════════════════════════════════════════════════════════════
│░░░░░░░░│░░░░░░░░│████████████████████████│ Semanas 4-6
└─────────────────────────────────────────────────────────────→

Tareas Semana 4:
✓ Crear TimelineVisualizerView.xaml [3d]
  - Layout básico (Grid, Canvas, ScrollViewer)
  - Controls: Play, Pause, Stop, Slider
  - Event details panel

✓ TimelineVisualizerViewModel.cs [2d]
  - PlayCommand, PauseCommand, StopCommand
  - CurrentTime property
  - SelectedEvent property
  - Data binding setup

Tareas Semana 5:
✓ Renderizado de timeline en Canvas [4d]
  - DrawChannel() method
  - DrawEvent() method
  - Color coding por tipo de señal
  - Time axis rendering

✓ Interactividad [1d]
  - Mouse wheel zoom
  - Pan/scroll
  - Click to select event

Tareas Semana 6:
✓ Visualización avanzada [3d]
  - Waveform preview en eventos
  - Tooltips con info detallada
  - Highlight de overlaps/conflicts
  - Real-time playback indicator

✓ Polish UI [2d]
  - Styling (colores, fuentes)
  - Responsive layout
  - Dark/Light theme support

Entregables:
- Timeline totalmente funcional
- Interacción fluida
- Visualización clara de señales

────────────────────────────────────────────────────────────────

FASE 3: SIGNAL LIBRARY (Semanas 7-8.5)
════════════════════════════════════════════════════════════════
│░░░░░░░░│░░░░░░░░│░░░░░░░░│████████████│ Semanas 7-8.5
└─────────────────────────────────────────────────────────────→

Tareas Semana 7:
✓ SignalLibrary.cs [2d]
  - LoadLibrary() from JSON
  - SaveLibrary() to JSON
  - GetSignalsByCategory()
  - AddSignal(), RemoveSignal()

✓ Signal templates [3d]
  - Crear 20+ señales predefinidas
  - Categorías: DC, Ramp, Waveform, Digital
  - JSON files en Resources/SignalLibrary/

Tareas Semana 8-8.5:
✓ SignalLibraryView.xaml [2d]
  - TreeView para categorías
  - Preview de señal seleccionada
  - Drag & drop a timeline

✓ Signal Editor Dialog [1.5d]
  - Crear/editar señales custom
  - Parameter validation
  - Preview en tiempo real

Entregables:
- Biblioteca con 20+ señales
- UI drag & drop funcional
- Editor de señales custom

────────────────────────────────────────────────────────────────

FASE 4: EXECUTION ENGINE (Semanas 9-10)
════════════════════════════════════════════════════════════════
│░░░░░░░░│░░░░░░░░│░░░░░░░░│░░░░░░░░│████████████████│ Sem 9-10
└─────────────────────────────────────────────────────────────→

Tareas Semana 9:
✓ ExecutionEngine.cs [4d]
  - ExecuteSequenceAsync()
  - WaitUntilAsync() con alta precisión
  - ExecuteEventAsync() multi-tipo
  - Pause/Resume/Stop

✓ Sincronización multi-canal [1d]
  - Parallel event execution
  - Conflict detection
  - Resource locking

Tareas Semana 10:
✓ Error handling & recovery [2d]
  - Try-catch por evento
  - Rollback on critical error
  - Logging comprehensivo

✓ Real-time feedback [2d]
  - Progress updates a UI
  - Current event highlight
  - Performance metrics

✓ Tests de ejecución [1d]
  - ExecutionEngineTests.cs
  - Integration tests con hardware

Entregables:
- Ejecución de secuencias funcional
- Timing preciso (<5ms error)
- Manejo robusto de errores

────────────────────────────────────────────────────────────────

FASE 5: INTEGRATION & TESTING (Semanas 11-12)
════════════════════════════════════════════════════════════════
│░░░░░░░░│░░░░░░░░│░░░░░░░░│░░░░░░░░│░░░░░░░░│████████████████│
└─────────────────────────────────────────────────────────────→

Tareas Semana 11:
✓ Integración con MainWindow [2d]
  - Agregar menú "Signal Manager"
  - Navigation a ventana nueva
  - State management

✓ Integración con DAQController [2d]
  - Conectar ExecutionEngine
  - Validación de dispositivos
  - Hardware availability check

✓ End-to-end testing [1d]
  - Crear secuencia → Visualizar → Ejecutar
  - Test con ambas tarjetas
  - Performance profiling

Tareas Semana 12:
✓ Bug fixing [3d]
  - Resolver issues encontrados
  - Memory leaks check
  - Thread safety audit

✓ User acceptance testing [2d]
  - Crear 3 secuencias de ejemplo
  - Test con usuario final
  - Feedback incorporation

Entregables:
- Sistema completamente integrado
- Bugs críticos resueltos
- 3 secuencias de ejemplo funcionales

────────────────────────────────────────────────────────────────

FASE 6: DOCUMENTATION & POLISH (Semana 13)
════════════════════════════════════════════════════════════════
│░░░░░░░░│░░░░░░░░│░░░░░░░░│░░░░░░░░│░░░░░░░░│░░░░░░░░│████████│
└─────────────────────────────────────────────────────────────→

Tareas:
✓ Documentación técnica [2d]
  - SIGNAL_MANAGER_ARCHITECTURE.md
  - SIGNAL_MANAGER_USER_GUIDE.md
  - API_REFERENCE update

✓ Comentarios XML [1d]
  - XML docs en todas las clases públicas
  - IntelliSense completo
  - Ejemplos de uso

✓ Polish final [2d]
  - Iconos y recursos gráficos
  - Tooltips y ayuda contextual
  - Keyboard shortcuts

Entregables:
- Documentación completa
- Sistema listo para producción
- Training materials

════════════════════════════════════════════════════════════════
```

### Timeline Visual Compacto

```
Fase 0: Setup          │█│
Fase 1: Sequence Eng   │░│██│
Fase 2: Timeline UI    │░│░░│███│
Fase 3: Signal Library │░│░░│░░░│██│
Fase 4: Execution Eng  │░│░░│░░░│░░│██│
Fase 5: Integration    │░│░░│░░░│░░│░░│██│
Fase 6: Documentation  │░│░░│░░░│░░│░░│░░│█│
                       └─┴──┴───┴───┴──┴──┴──┘
                       W1 W3  W6  W8 W10 W12 W13
```

### Hitos Críticos (Milestones)

| Semana | Hito | Criterio de Éxito |
|--------|------|-------------------|
| 1 | ✅ Aprobación de propuesta | Documento aprobado por usuario |
| 3 | 🎯 Core Engine completo | Tests pasando, JSON I/O funcional |
| 6 | 🎯 UI Timeline operacional | Visualización y edición básica working |
| 8.5 | 🎯 Signal Library integrada | Drag & drop funcional, 20+ señales |
| 10 | 🎯 Execution Engine funcional | Secuencias ejecutándose con timing correcto |
| 12 | 🎯 System Integration completa | End-to-end workflow completo |
| 13 | 🚀 Release v1.0 | Documentación completa, sistema en producción |

---

## 💰 ESTIMACIÓN DE RECURSOS

### Esfuerzo por Categoría

| Categoría | Horas | % Total | Prioridad |
|-----------|-------|---------|-----------|
| Backend (Core) | 140h | 28% | CRÍTICA |
| Frontend (UI) | 180h | 36% | ALTA |
| Testing & QA | 80h | 16% | CRÍTICA |
| Integration | 60h | 12% | CRÍTICA |
| Documentation | 40h | 8% | MEDIA |
| **TOTAL** | **500h** | **100%** | |

### Equipo Sugerido

- **1 Developer Full-Time** → 12.5 semanas (~3 meses)
- **O 2 Developers** → 6-7 semanas (~1.5 meses)

### Dependencias Externas

### Mitigación Detallada

#### **Timing Drift**
```csharp
// Compensación acumulativa
long targetTicks = startTicks + (eventIndex * ticksPerEvent);
while (stopwatch.ElapsedTicks < targetTicks) { /* wait */ }
// En vez de:
// await Task.Delay(eventDuration); // Se acumula error
```

#### **Performance de Timeline UI**
- Renderizar solo eventos visibles en viewport
- Usar `ItemsControl` con virtualización
- Throttling de eventos de mouse (debouncing)
- Canvas layers: background → events → playhead

### Riesgos de Proyecto

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| **Scope creep** | Alta | Alto | Roadmap claro + revisión semanal |
| **Requerimientos cambiantes** | Media | Medio | Sprints cortos + feedback continuo |
| **Integración compleja** | Media | Alto | Tests de integración desde Fase 1 |
| **Falta de tiempo** | Media | Alto | Priorización estricta + MVP primero |

---

## 💼 CASOS DE USO

### Caso de Uso 1: Alineación de Láser

**Objetivo:** Rampa gradual de voltaje para alinear láser sin dañar detector

**Secuencia:**
1. t=0s: Ramp 0V → 2V en 2s (Ch0, Analog)
2. t=2s: Hold 2V durante 5s
3. t=7s: Ramp 2V → 5V en 3s
4. t=10s: Hold 5V (indefinido)

**Beneficio:**
- Antes: Manual, requiere 2 personas
- Después: Automatizado, reproducible, 1 persona

---

### Caso de Uso 2: Sincronización Shutter + Modulación

**Objetivo:** Controlar shutter digital mientras se modula señal analógica

**Secuencia:**
1. t=0s: Digital P0 ON (abrir shutter)
2. t=0.1s: Start sine 100Hz, 5V amp, 5V offset (Ch0, Analog)
3. t=5.0s: Stop sine
4. t=5.1s: Digital P0 OFF (cerrar shutter)

**Beneficio:**
- Sincronización precisa (<5ms)
- Evita exposición no deseada
- Repetible en múltiples experimentos

---

### Caso de Uso 3: Pulse Train para Espectroscopía

**Objetivo:** Generar tren de pulsos con timing preciso para medición

**Secuencia:**
```
Ch0 (Analog): ████░░░░████░░░░████░░░░  (3x pulsos 1s ON, 1s OFF)
P0 (Digital): ░██░░██░░██░░██░░██░░██░  (6x trigger 100ms ON, 200ms OFF)
```

**Beneficio:**
- Timing hardware preciso
- Visualización clara de relación temporal
- Fácil ajuste de parámetros

---

## 🎯 BENEFICIOS ESPERADOS

### Beneficios Inmediatos

1. **Productividad:** Reducción 60% en tiempo de setup de experimentos
2. **Reproducibilidad:** Secuencias guardadas = experimentos idénticos
3. **Seguridad:** Validación previa evita daños a equipos
4. **Documentación:** Timeline visual = documentación automática

### Beneficios a Largo Plazo

1. **Biblioteca de Experimentos:** Acumulación de know-how
2. **Onboarding:** Nuevos usuarios aprenden viendo secuencias
3. **Colaboración:** Compartir secuencias entre equipos
4. **Escalabilidad:** Agregar nuevos dispositivos sin cambiar workflow

### KPIs Medibles

| KPI | Baseline | Meta (3 meses) | Medición |
|-----|----------|----------------|----------|
| Tiempo setup experimento | 30 min | 10 min | Timer manual |
| Errores en configuración | 15%/día | 2%/día | Log de errores |
| Secuencias reutilizadas | 0 | 20+ | Contador en app |
| Usuarios activos | 1 | 3-5 | Analytics |

---

## 🚀 ROADMAP POST-V1.0

### Versión 1.1 (Q2 2026)
- Import/Export a otros formatos (CSV, XML)
- Remote execution via API REST
- Cloud storage de secuencias

### Versión 1.2 (Q3 2026)
- Machine learning para optimización automática
- Feedback loop: sensores → ajuste en tiempo real
- Multi-experiment scheduling

### Versión 2.0 (Q4 2026)
- Web interface (ASP.NET Core + Blazor)
- Mobile monitoring app
- Collaborative editing (múltiples usuarios simultáneos)

---

## ✅ PROCESO DE APROBACIÓN

### Opciones de Decisión

Este documento requiere aprobación formal antes de comenzar implementación.

#### **OPCIÓN A: APROBAR SIN CAMBIOS**
- Proceder con Fase 0 inmediatamente
- Timeline: Inicio en semana siguiente
- Compromiso: 12.5 semanas de desarrollo

#### **OPCIÓN B: APROBAR CON MODIFICACIONES**
Especificar cambios requeridos en:
- [ ] Alcance (agregar/quitar features)
- [ ] Timeline (ajustar duración)
- [ ] Prioridades (cambiar orden de fases)
- [ ] Recursos (equipo, herramientas)

#### **OPCIÓN C: APROBAR VERSIÓN REDUCIDA (MVP)**
Implementar solo lo esencial:
- Fase 0-1: Sequence Engine ✓
- Fase 2 (parcial): Timeline básico (sin drag&drop)
- Fase 4: Execution Engine ✓
- **Total: 6-7 semanas** en vez de 12.5

#### **OPCIÓN D: RECHAZAR / POSPONER**
Motivos:
- Prioridades diferentes
- Falta de recursos
- Necesita más análisis

---

### Checklist de Aprobación

Antes de aprobar, verificar:

- [ ] **Objetivos claros:** ¿El Signal Manager resuelve el problema planteado?
- [ ] **Timeline realista:** ¿12.5 semanas es aceptable?
- [ ] **Recursos disponibles:** ¿Hay developer(s) disponible(s)?
- [ ] **Riesgos aceptables:** ¿Los riesgos están bien mitigados?
- [ ] **ROI positivo:** ¿Beneficios justifican inversión?
- [ ] **Casos de uso validados:** ¿Los ejemplos reflejan necesidades reales?

---

## 📝 FIRMA Y APROBACIÓN

**Decisión:**  
☐ OPCIÓN A: Aprobar sin cambios  
☐ OPCIÓN B: Aprobar con modificaciones (especificar abajo)  
☐ OPCIÓN C: Aprobar versión reducida (MVP)  
☐ OPCIÓN D: Rechazar / Posponer

**Modificaciones/Comentarios:**
```
_________________________________________________________________

_________________________________________________________________

_________________________________________________________________
```

**Fecha de Aprobación:** ____________________

**Firma/Aprobación:** ________________________

**Fecha de Inicio Estimada:** ________________

---

## 📚 REFERENCIAS

### Papers y Publicaciones
1. ArXiv:2406.17603v1 - "Experimental timing and control using microcontrollers"
2. Brookhaven National Lab - "Bluesky: Data Acquisition and Analysis Framework"
3. National Instruments - "LabVIEW Waveform Sequencer Library"

### Proyectos Open Source
1. openDAQ - https://github.com/openDAQ/openDAQ
2. PyMeasure - https://github.com/pymeasure/pymeasure
3. Prawnblaster - https://github.com/labscript-suite

### Documentación Técnica Interna
1. `AUDIT_COMPLETO_2026-03-09.md` - Auditoría del sistema actual
2. `SDK_HARDWARE_SPECS.md` - Especificaciones de hardware
3. `API_REFERENCE.md` - API actual de LAMP DAQ Control

---

## 📧 CONTACTO Y SEGUIMIENTO

**Documentación creada por:** Cascade AI Assistant  
**Fecha:** 2026-03-09 15:10:00  
**Ubicación:** `c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\Core\Docs\SIGNAL_MANAGER_PROPOSAL_2026-03-09_151000.md`

**Para aprobación/consultas:**
- Revisar este documento completo
- Completar sección de aprobación arriba
- Comunicar decisión para proceder

**Próximos pasos después de aprobación:**
1. Setup de estructura de proyecto (Fase 0)
2. Kickoff meeting
3. Sprint planning semanal
4. Updates de progreso regulares

---

**FIN DEL DOCUMENTO**
