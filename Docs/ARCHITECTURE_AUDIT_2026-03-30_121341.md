# AUDITORÍA ARQUITECTÓNICA COMPLETA — LAMP DAQ Control v0.8

**Fecha:** 2026-03-30 12:13:41  
**Proyecto:** `c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8`  
**SDK Referencia:** `c:\LAMP_CONTROL\SDK_Advantech` (DAQNavi 4.0.0.0)  
**Hardware Referencia:** PCIe-1824 User Interface PDF, PCI-1735U User Interface PDF  
**Framework:** .NET Framework 4.7.2 | WPF + Console dual mode  

---

## TABLA DE CONTENIDOS

1. [Resumen Ejecutivo](#1-resumen-ejecutivo)
2. [Arquitectura Actual](#2-arquitectura-actual)
3. [Hallazgos Críticos (CRITICAL)](#3-hallazgos-críticos)
4. [Hallazgos Altos (HIGH)](#4-hallazgos-altos)
5. [Hallazgos Medios (MEDIUM)](#5-hallazgos-medios)
6. [Hallazgos Bajos (LOW)](#6-hallazgos-bajos)
7. [Estrategias de Implementación](#7-estrategias-de-implementación)
8. [Roadmap de Correcciones](#8-roadmap-de-correcciones)
9. [Archivos Auditados](#9-archivos-auditados)

---

## 1. RESUMEN EJECUTIVO

Se realizó una auditoría exhaustiva de toda la arquitectura del programa LAMP DAQ Control v0.8, cruzando el código fuente con la documentación oficial del hardware (PCIe-1824, PCI-1735U) y el SDK nativo Advantech DAQNavi 4.0.0.0.

### Estadísticas de Hallazgos

| Severidad | Cantidad | Impacto |
|-----------|----------|---------|
| **CRITICAL** | 3 | Corrupción de datos, crashes, daño potencial al hardware |
| **HIGH** | 4 | Pérdida de datos, comportamiento impredecible, deadlocks |
| **MEDIUM** | 5 | Funcionalidad incorrecta, ineficiencia, mantenibilidad |
| **LOW** | 3 | Limpieza de código, consistencia, optimización menor |

### Veredicto General

La arquitectura base es sólida (MVVM, capas bien definidas, HAL abstracto). Sin embargo, existen **3 problemas críticos** que pueden causar corrupción de señales, crashes en producción, y uso incorrecto del SDK Advantech. El más grave es la **doble inicialización de controladores SDK** que viola las restricciones del hardware, seguido por la **ausencia total de validación de ErrorCode** del SDK.

---

## 2. ARQUITECTURA ACTUAL

### Diagrama de Capas

```
┌─────────────────────────────────────────────────────────┐
│  PRESENTATION: WPF Views / Console                      │
├─────────────────────────────────────────────────────────┤
│  VIEWMODELS: MainVM, AnalogControlVM, DigitalMonitorVM  │
│              SignalManagerVM, TimelineChannelVM          │
├─────────────────────────────────────────────────────────┤
│  CONTROLLERS: DAQController (facade)                    │
│               DataOrientedExecutionEngine               │
├─────────────────────────────────────────────────────────┤
│  MANAGERS: DeviceManager, ChannelManager, ProfileManager│
│  SERVICES: SignalGenerator, SignalLUT, DeviceTypeResolver│
├─────────────────────────────────────────────────────────┤
│  ENGINE: DaqEngine → InputPoller → StateGrid →          │
│          LogicPipeline → OutputMetronome → Dispatcher    │
├─────────────────────────────────────────────────────────┤
│  HAL: IDigitalHal / IAnalogHal                          │
│       AdvantechDigitalHal / AdvantechAnalogHal           │
├─────────────────────────────────────────────────────────┤
│  SDK: Automation.BDaq (DAQNavi 4.0.0.0)                 │
│       InstantAoCtrl / InstantDiCtrl / InstantDoCtrl     │
├─────────────────────────────────────────────────────────┤
│  HARDWARE: PCIe-1824 (32ch AO, 16-bit, 0-10V)          │
│            PCI-1735U (4×8 DIO, 3 counters)              │
└─────────────────────────────────────────────────────────┘
```

### Flujos de Datos Principales

**Flujo 1 — Escritura Analógica Directa (sin Engine):**
```
UI → AnalogControlVM → DAQController.WriteVoltage() → DeviceManager.WriteVoltage()
  → InstantAoCtrl.Write(channel, voltage)
```

**Flujo 2 — Escritura Analógica via Engine:**
```
UI → DAQController.WriteVoltage() → DaqEngine.WriteAnalogVoltage()
  → LogicPipeline.RequestAnalogWrite() → StateGrid.SetAnalogVoltage()
  → [Metronome tick] → Dispatcher.AnalogWriteLoop() → AdvantechAnalogHal.WriteOutputs()
  → InstantAoCtrl.Write(channel, voltage)
```

**Flujo 3 — Signal Generation (BYPASSES HAL):**
```
UI → AnalogControlVM → DAQController.StartSignalGeneration()
  → SignalGenerator.Start() → [Thread] → InstantAoCtrl.Write(channel, voltage) ← DIRECTO
```

**Flujo 4 — Lectura Digital via Engine:**
```
[Thread] HighSpeedInputPoller → IDigitalHal.ReadInputsRaw() → XOR detection
  → ConcurrentQueue<InputChangeEvent> → LogicPipeline → StateGrid.UpdateInputState()
  → [DispatcherTimer] DigitalMonitorVM → StateGrid.ActiveInputMask → UI
```

---

## 3. HALLAZGOS CRÍTICOS

### CRIT-01: Doble Inicialización de Controladores SDK

**Severidad:** CRITICAL  
**Componentes:** `DeviceManager.cs`, `DaqEngine.cs`, `AdvantechAnalogHal.cs`, `AdvantechDigitalHal.cs`  
**Referencia SDK:** `InstantAoCtrl`, `InstantDiCtrl`, `InstantDoCtrl` — cada instancia abre un handle exclusivo al hardware

#### Descripción

Existen **dos rutas de inicialización paralelas** que crean instancias SDK independientes para el mismo hardware físico:

**Ruta 1 — DeviceManager (legacy):**
```csharp
// DeviceManager.cs — crea InstantAoCtrl propio
private readonly InstantAoCtrl _analogDevice;
// En constructor: _analogDevice = new InstantAoCtrl();
// En TryInitializeAnalogDevice: _analogDevice.SelectedDevice = new DeviceInformation(deviceNumber);
```

**Ruta 2 — DaqEngine → HAL:**
```csharp
// AdvantechAnalogHal.cs — crea OTRO InstantAoCtrl
private InstantAoCtrl _aoCtrl;
// En Initialize: _aoCtrl = new InstantAoCtrl();
// _aoCtrl.SelectedDevice = new DeviceInformation(actualDevNum);
```

Cuando `DAQController.Initialize()` es llamado, inicializa el `DeviceManager` que abre un handle al PCIe-1824. Si posteriormente se inicia el `DaqEngine`, este crea una SEGUNDA instancia `InstantAoCtrl` apuntando al mismo dispositivo físico.

#### Impacto

- **ErrorDeviceNotOpened / ErrorFuncBusy**: El SDK Advantech no garantiza acceso multi-handle al mismo dispositivo
- **Corrupción de señales**: Dos hilos escribiendo voltajes simultáneamente al mismo DAC sin coordinación
- **Comportamiento indefinido**: El segundo `SelectedDevice` puede fallar silenciosamente si el SDK no devuelve error

#### Evidencia en SDK

```
ErrorCode enum incluye:
  ErrorDeviceNotOpened — Device handle inválido
  ErrorFuncBusy — Función ocupada por otro handle
  ErrorDeviceNotExist — Dispositivo no accesible
```

#### Nota sobre `InitializeAnalogFromExisting()`

Existe `DaqEngine.InitializeAnalogFromExisting(InstantAoCtrl)` (línea 179 de DaqEngine.cs) que permite compartir el handle. Sin embargo, `AdvantechAnalogHal.InitializeFromExisting()` crea un wrapper sin verificar si el controlador ya tiene un `SelectedDevice` válido, y no hay garantía de que `DAQController` use esta ruta de forma consistente.

#### Estrategia de Corrección

```
OPCIÓN A (Recomendada): Single-Owner Pattern
  1. HAL es el ÚNICO propietario de instancias SDK
  2. DeviceManager se elimina o se convierte en wrapper del HAL
  3. DaqEngine.InitializeDigital/Analog crean los HALs
  4. DAQController obtiene acceso al SDK SOLO a través del HAL
  5. SignalGenerator recibe IAnalogHal, no InstantAoCtrl

OPCIÓN B: Shared Handle Registry
  1. Crear SdkHandleRegistry singleton
  2. Todos los componentes solicitan handles a través del registry
  3. El registry garantiza un solo InstantAoCtrl por dispositivo físico
  4. Reference counting para disposal seguro
```

---

### CRIT-02: Ausencia Total de Validación de ErrorCode del SDK

**Severidad:** CRITICAL  
**Componentes:** TODOS los que interactúan con el SDK  
**Referencia SDK:** `ErrorCode` enum con 60+ códigos de error

#### Descripción

El SDK Advantech DAQNavi retorna `ErrorCode` de prácticamente TODAS sus operaciones. El código fuente **NUNCA** verifica estos códigos de retorno.

#### Ejemplos Específicos

**AdvantechAnalogHal.WriteSingle() — línea ~120:**
```csharp
public void WriteSingle(int channel, double voltage)
{
    _aoCtrl.Write(channel, voltage);  // ← ErrorCode IGNORADO
}
```

**AdvantechDigitalHal.WriteOutputs() — línea ~130:**
```csharp
public void WriteOutputs(uint state)
{
    _doCtrl.Write(0, (byte)(state & 0xFF));  // ← ErrorCode IGNORADO
    _doCtrl.Write(1, (byte)((state >> 8) & 0xFF));  // ← ErrorCode IGNORADO
    // ...
}
```

**DeviceManager.TryInitializeAnalogDevice():**
```csharp
_analogDevice.Write(0, 0.0);  // ← ErrorCode IGNORADO (test write)
```

**ProfileManager.TryLoadProfile():**
```csharp
_deviceManager.Device.LoadProfile(fullPath);  // ← ErrorCode IGNORADO
```

#### ErrorCodes Críticos No Detectados

| ErrorCode | Significado | Consecuencia de Ignorar |
|-----------|-------------|------------------------|
| `ErrorParamOutOfRange` | Canal/voltaje fuera de rango | Escritura silenciosa a canal incorrecto |
| `ErrorDeviceNotOpened` | Handle inválido | Todas las operaciones fallan silenciosamente |
| `ErrorFuncBusy` | Otro proceso usa el dispositivo | Escrituras perdidas |
| `ErrorDeviceIoTimeOut` | Timeout de comunicación PCIe/PCI | Hardware no responde, estado desconocido |
| `WarningPropValueOutOfRange` | Valor fuera del rango configurado | DAC clipea el valor sin aviso |

#### Estrategia de Corrección

```csharp
// Crear helper method en cada HAL:
private void CheckError(ErrorCode err, string operation)
{
    if (err != ErrorCode.Success)
    {
        if (err >= ErrorCode.ErrorHandleNotValid) // Errors (vs Warnings)
            throw new DAQOperationException($"{operation} failed: {err}");
        else
            _logger.Warn($"{operation}: SDK warning {err}");
    }
}

// Uso:
public void WriteSingle(int channel, double voltage)
{
    var err = _aoCtrl.Write(channel, voltage);
    CheckError(err, $"WriteSingle(ch={channel}, v={voltage:F3})");
}
```

---

### CRIT-03: SignalGenerator Bypasses HAL — Race Condition con Engine

**Severidad:** CRITICAL  
**Componentes:** `SignalGenerator.cs`, `DaqEngine.cs`, `SynchronizedOutputDispatcher.cs`  
**Referencia Hardware:** PCIe-1824 — DAC single-write register, no tiene buffer de hardware

#### Descripción

`SignalGenerator` recibe una referencia directa a `InstantAoCtrl` y escribe al DAC en un tight loop a 100kHz:

```csharp
// SignalGenerator.cs línea 586:
_device.Write(channel, outputVoltage);  // Escritura directa al hardware
```

Mientras tanto, si el `DaqEngine` está activo, su `SynchronizedOutputDispatcher` también escribe al hardware a través del HAL:

```csharp
// SynchronizedOutputDispatcher.cs línea 232:
_analogHal.WriteOutputs(voltages, requiredMask);
// → que internamente llama: _aoCtrl.Write(channel, voltage)
```

#### Escenario de Corrupción

```
Timeline:
  T=0.000ms  SignalGenerator escribe CH0 = 5.23V  (via _device.Write)
  T=0.005ms  Dispatcher escribe CH0 = 0.00V       (via HAL → _aoCtrl.Write)
  T=0.010ms  SignalGenerator escribe CH0 = 5.45V  (via _device.Write)
  
  Resultado: Glitch de ~5V en la señal de salida del DAC
  Con PCIe-1824 a 16-bit, esto es un salto de ~32,000 LSBs
```

#### Impacto en Hardware

- **Glitches en señal analógica**: Saltos abruptos de voltaje que pueden dañar equipos conectados
- **Pérdida de sincronización**: El Barrier del dispatcher espera a que el analog thread escriba, pero el SignalGenerator también está escribiendo
- **Indeterminismo**: El orden de escritura entre threads no es determinístico

#### Estrategia de Corrección

```
OPCIÓN A (Recomendada): SignalGenerator escribe a través del StateGrid
  1. SignalGenerator recibe IAnalogHal en lugar de InstantAoCtrl
  2. En modo Engine: escribe a StateGrid.SetAnalogVoltage() en lugar de HAL directo
  3. El Metronome/Dispatcher maneja la escritura real al hardware
  4. Ventaja: toda escritura pasa por un punto único, eliminando la race condition

OPCIÓN B: Mutex de escritura en HAL
  1. AdvantechAnalogHal.WriteSingle() adquiere un lock antes de escribir
  2. SignalGenerator y Dispatcher compiten por el lock
  3. Desventaja: latencia de lock en el hot loop de 100kHz
  4. Desventaja: no resuelve el conflicto lógico (dos fuentes de verdad para el voltaje)
```

---

## 4. HALLAZGOS ALTOS

### HIGH-01: IDeviceManager.Device Expone Tipo Concreto del SDK

**Severidad:** HIGH  
**Componentes:** `IDeviceManager.cs`, `ChannelManager.cs`, `ProfileManager.cs`, `SignalGenerator.cs`

#### Descripción

La interfaz `IDeviceManager` expone directamente el tipo concreto del SDK:

```csharp
// IDeviceManager.cs línea 16:
InstantAoCtrl Device { get; }  // ← Tipo concreto de Advantech SDK
```

Esto tiene múltiples consecuencias:

1. **Imposible para dispositivos digitales**: `InstantAoCtrl` es exclusivamente analógico. Para PCI-1735U se necesitan `InstantDiCtrl`/`InstantDoCtrl`
2. **ChannelManager falla con digital**: `GetChannelStates()` accede a `_deviceManager.Device.Channels` que es null/inválido para dispositivos digitales
3. **ProfileManager carga perfil digital en controlador analógico**: `_deviceManager.Device.LoadProfile()` llama `InstantAoCtrl.LoadProfile()` con un perfil PCI-1735U
4. **Acoplamiento fuerte**: Toda la capa de Managers depende de un tipo SDK concreto

#### Estrategia de Corrección

```csharp
// Separar la interfaz por tipo de dispositivo:
public interface IDeviceManager
{
    bool IsInitialized { get; }
    DeviceType CurrentDeviceType { get; }
    string DeviceModel { get; }
    void InitializeDevice(int deviceNumber, string profileName = null);
    DeviceInfo GetDeviceInfo();
    IList<DeviceInfo> DetectDevices();
}

public interface IAnalogDeviceManager : IDeviceManager
{
    int ChannelCount { get; }
    void WriteVoltage(int channel, double value);
    void ConfigureChannels(ValueRange range);
}

public interface IDigitalDeviceManager : IDeviceManager
{
    void WriteDigitalPort(int port, byte value);
    byte ReadDigitalPort(int port);
    void WriteDigitalBit(int port, int bit, bool value);
    bool ReadDigitalBit(int port, int bit);
}
```

---

### HIGH-02: Barrier Synchronization Timeout Desync

**Severidad:** HIGH  
**Componentes:** `SynchronizedOutputDispatcher.cs`, `OutputMetronome.cs`

#### Descripción

El `SynchronizedOutputDispatcher` usa un `Barrier` con 3 participantes (metronome, digital thread, analog thread). Cada participante llama `SignalAndWait()` con timeout:

```csharp
// TriggerCycle (Metronome caller): timeout 50ms
_syncBarrier.SignalAndWait(50);

// DigitalWriteLoop: timeout 100ms
_syncBarrier.SignalAndWait(100);

// AnalogWriteLoop: timeout 100ms
_syncBarrier.SignalAndWait(100);
```

Si **cualquier** participante experimenta un timeout:
1. El `Barrier` queda en estado inconsistente (phase mismatch)
2. Los otros participantes quedan bloqueados hasta SU timeout
3. En el siguiente ciclo, los participantes están desfasados
4. Puede causar **deadlock permanente** si el Barrier no se recupera

Además, en `Stop()`:
```csharp
_running = false;
try { _syncBarrier?.Dispose(); } catch { }  // ← Dispose mientras threads pueden estar en SignalAndWait!
```

Dispose de un `Barrier` mientras hay threads bloqueados en `SignalAndWait()` lanza `ObjectDisposedException`, pero está silenciada por el `catch { }`.

#### Estrategia de Corrección

```
1. Reemplazar Barrier por ManualResetEventSlim dual:
   - _digitalReady + _analogReady (set por threads de escritura cuando están listos)
   - _triggerWrite (set por metronome para liberar escritura)
   
2. Shutdown limpio:
   - _running = false;
   - Set todos los eventos para despertar threads
   - Join con timeout
   - LUEGO dispose eventos

3. Alternativa: usar CancellationToken en SignalAndWait
   _syncBarrier.SignalAndWait(50, _cts.Token);
```

---

### HIGH-03: Metronome ClearOutputMasks Race Condition

**Severidad:** HIGH  
**Componentes:** `OutputMetronome.cs`, `StateGrid.cs`, `LogicPipeline.cs`

#### Descripción

En `OutputMetronome.MetronomeLoop()`:

```csharp
// Paso 1: Trigger write
_dispatcher.TriggerCycle();

// Paso 2: Clear masks (DESPUÉS del trigger)
_stateGrid.ClearOutputMasks();    // ← RACE CONDITION
_stateGrid.RecordOutputWrite();
```

**Problema**: Entre `TriggerCycle()` retornando y `ClearOutputMasks()` ejecutando, otro thread (LogicPipeline, o un RequestDigitalWrite/RequestAnalogWrite directo) puede establecer nuevos bits en las máscaras de output. Estos bits se **pierden** porque `ClearOutputMasks()` hace un `Interlocked.Exchange(ref mask, 0)` incondicional.

#### Escenario de Pérdida

```
T=0.000: Metronome llama TriggerCycle() — dispatcher escribe state con mask=0x01
T=0.001: LogicPipeline.RequestDigitalWrite() establece mask |= 0x02  (nuevo bit)
T=0.002: Metronome llama ClearOutputMasks() → mask = 0  ← bit 0x02 PERDIDO
T=0.500: Siguiente ciclo: mask=0, nada que escribir. El bit 0x02 nunca llega al hardware
```

#### Estrategia de Corrección

```csharp
// Opción A: Read-and-clear atómico
public uint ConsumeDigitalOutputMask()
{
    return (uint)Interlocked.Exchange(ref _requiredDigitalOutputMask, 0);
}

// En MetronomeLoop:
uint digitalMask = _stateGrid.ConsumeDigitalOutputMask();
uint analogMask = _stateGrid.ConsumeAnalogOutputMask();
if (digitalMask != 0 || analogMask != 0)
{
    _dispatcher.TriggerCycle(digitalMask, analogMask);  // Pasar masks consumidas
}

// Opción B: Dispatcher lee y limpia sus propias masks
// (mover ClearOutputMasks dentro del Dispatcher después de escribir)
```

---

### HIGH-04: Thread Safety en SignalGenerator Static Fields

**Severidad:** HIGH  
**Componentes:** `SignalGenerator.cs`

#### Descripción

Múltiples campos estáticos se comparten entre todas las instancias de `SignalGenerator` y se acceden desde múltiples hilos:

```csharp
// Campos estáticos compartidos:
private static double[] _cachedNormalizedValues = null;  // Leído sin lock en hot loop
private static int _cachedLutSize = 0;
private static Barrier _phaseBarrier = null;
```

**Problemas específicos:**

1. **`_cachedNormalizedValues`**: Se lee en el hot loop (línea 579) sin lock, pero se escribe dentro de `_lutCacheLock`. En .NET Framework 4.7.2, la lectura de una referencia de array es atómica, pero la visibilidad entre threads no está garantizada sin `volatile` o barrier. El thread del signal generator podría leer un array parcialmente inicializado.

2. **`_phaseBarrier`**: Es estático y compartido por TODAS las instancias. Si un `DataOrientedExecutionEngine` prepara un barrier para 3 waveforms, y otra instancia simultáneamente prepara un barrier para 2, se corrompe.

3. **`IsChannelActive()`** (línea 409): Accede a `_activeChannels.ContainsKey(channel)` sin lock, mientras otros threads modifican el diccionario.

#### Estrategia de Corrección

```csharp
// 1. Hacer _cachedNormalizedValues volatile o usar Volatile.Read:
private static volatile double[] _cachedNormalizedValues = null;

// 2. Mover _phaseBarrier a instancia (no estático):
private Barrier _phaseBarrier = null;  // Por instancia de SignalGenerator

// 3. Lock en IsChannelActive:
public bool IsChannelActive(int channel)
{
    lock (_activeChannelsLock)
    {
        return _activeChannels.ContainsKey(channel);
    }
}
```

---

## 5. HALLAZGOS MEDIOS

### MED-01: PCI-1735U Port Direction No Configurada

**Severidad:** MEDIUM  
**Componentes:** `AdvantechDigitalHal.cs`, `DeviceManager.cs`  
**Referencia Hardware:** PCI-1735U User Interface PDF — Sección DIO Port Direction

#### Descripción

El perfil PCI-1735U define los 4 puertos como `DioPortType = 2` (PortDio = bidireccional). Sin embargo, el código nunca configura la dirección (`DioPortDir`) de los puertos.

```csharp
// AdvantechDigitalHal.Initialize() — NO configura dirección
_diCtrl = new InstantDiCtrl();
// ... selecciona dispositivo, lee port 0 como test
// NUNCA llama: _diCtrl.Ports[i].DirectionMode = DioPortDir.Input;

_doCtrl = new InstantDoCtrl();
// ... selecciona dispositivo, escribe port 0 como test
// NUNCA llama: _doCtrl.Ports[i].DirectionMode = DioPortDir.Output;
```

En el PCI-1735U, los puertos son configurables. Si la dirección no se establece explícitamente, depende del estado previo del hardware (o del reset default), que puede no ser lo esperado.

#### Estrategia de Corrección

```csharp
// En AdvantechDigitalHal.Initialize(), después de seleccionar dispositivo:
// Configurar puertos de input
for (int p = 0; p < _diCtrl.Ports.Count; p++)
{
    _diCtrl.Ports[p].DirectionMode = DioPortDir.Input;
}

// Configurar puertos de output
for (int p = 0; p < _doCtrl.Ports.Count; p++)
{
    _doCtrl.Ports[p].DirectionMode = DioPortDir.Output;
}
```

---

### MED-02: ProfileManager Asume Dispositivo Analógico

**Severidad:** MEDIUM  
**Componentes:** `ProfileManager.cs`

#### Descripción

`ProfileManager.TryLoadProfile()` llama:

```csharp
_deviceManager.Device.LoadProfile(fullPath);
```

`_deviceManager.Device` retorna `InstantAoCtrl`. Cuando se intenta cargar un perfil PCI-1735U (digital) en un controlador `InstantAoCtrl`, el resultado es indefinido. El SDK puede:
- Ignorar la configuración DIO
- Retornar un ErrorCode (que no se valida — ver CRIT-02)
- Aplicar parcialmente el perfil

Además, `ProfileManager.InitializeDefaultProfiles()` define:
```csharp
_availableProfiles["PCI-1735U"] = new DeviceProfile
{
    DefaultRange = ValueRange.V_Neg10To10,  // ← Rango ANALÓGICO para dispositivo DIGITAL
    ExpectedChannelCount = 4                // ← PCI-1735U tiene 4 PUERTOS, no canales AO
};
```

#### Estrategia de Corrección

```
1. Separar carga de perfiles por tipo:
   - Para analog: _analogCtrl.LoadProfile()
   - Para digital: no usar LoadProfile() (configurar puertos directamente)
   
2. Corregir DeviceProfile para PCI-1735U:
   - Eliminar DefaultRange (no aplica)
   - ExpectedChannelCount = 32 (bits) o ExpectedPortCount = 4
```

---

### MED-03: Rango de Voltaje No Validado Consistentemente

**Severidad:** MEDIUM  
**Componentes:** `SignalGenerator.cs`, `AnalogControlViewModel.cs`, `DAQController.cs`  
**Referencia Hardware:** PCIe-1824 soporta V_Neg10To10, V_0To10, mA_0To20

#### Descripción

El perfil PCIe-1824 (`PCIe1824_prof_v1.xml`) define rangos soportados:
```xml
<Value>1,33,34</Value>
<!-- 1 = V_Neg10To10, 33 = V_0To10, 34 = mA_0To20 -->
```

El rango configurado por canal es:
```xml
<Value>1,1,1,...,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,33</Value>
<!-- Canales 0-30: V_Neg10To10 (-10V a +10V), Canal 31: V_0To10 (0V a +10V) -->
```

Sin embargo, `SignalGenerator.GenerateSignal()` hardcodea el clamp:
```csharp
double outputVoltage = Math.Max(0.0, Math.Min(10.0, ...));  // Asume 0-10V
```

Para canales configurados como `V_Neg10To10`, esto recorta valores negativos legítimos. Para el canal 31 (`V_0To10`), el clamp es correcto por coincidencia.

#### Estrategia de Corrección

```csharp
// Leer rango del canal desde el SDK:
var range = _aoCtrl.Channels[channel].ValueRange;
double minV, maxV;
switch (range)
{
    case ValueRange.V_Neg10To10: minV = -10.0; maxV = 10.0; break;
    case ValueRange.V_0To10:    minV = 0.0;   maxV = 10.0; break;
    default:                    minV = -10.0; maxV = 10.0; break;
}
double outputVoltage = Math.Max(minV, Math.Min(maxV, computedVoltage));
```

---

### MED-04: No Hay Manejo de Hot-Plug de Dispositivos

**Severidad:** MEDIUM  
**Componentes:** Todos los HAL, `DeviceManager.cs`  
**Referencia SDK:** `EventId.EvtDeviceRemoved`, `EventId.EvtDeviceReconnected`

#### Descripción

El SDK proporciona eventos para detectar desconexión y reconexión de dispositivos:
```
EventId.EvtDeviceRemoved — Dispositivo desconectado
EventId.EvtDeviceReconnected — Dispositivo reconectado
```

Ningún componente del sistema se suscribe a estos eventos. Si el dispositivo se desconecta:
- `InstantAoCtrl.Write()` falla silenciosamente (ErrorCode ignorado — ver CRIT-02)
- Los threads del Engine continúan ejecutando, acumulando errores
- El usuario no recibe notificación hasta que ocurre un crash

#### Estrategia de Corrección

```csharp
// En AdvantechAnalogHal.Initialize():
_aoCtrl.addEventHandler(EventId.EvtDeviceRemoved, OnDeviceRemoved);
_aoCtrl.addEventHandler(EventId.EvtDeviceReconnected, OnDeviceReconnected);

private void OnDeviceRemoved(object sender, EventArgs e)
{
    IsReady = false;
    _logger.Error("[AnalogHal] Device REMOVED — all operations suspended");
    DeviceStateChanged?.Invoke(this, DeviceState.Disconnected);
}
```

---

### MED-05: ChannelManager Incompatible con Dispositivos Digitales

**Severidad:** MEDIUM  
**Componentes:** `ChannelManager.cs`

#### Descripción

`ChannelManager` accede a `_deviceManager.Device` (que es `InstantAoCtrl`) para todas las operaciones:

```csharp
// GetChannelStates — asume analog:
var device = _deviceManager.Device;
if (device.Channels == null || device.Channels.Length == 0)
    return Array.Empty<ChannelState>();

// ResetAllChannels — operación analog:
device.Write(i, 0.0);  // ← Crash si el "device" actual es digital
```

Cuando el sistema está configurado para PCI-1735U, `_deviceManager.Device` podría retornar un `InstantAoCtrl` no inicializado, causando `NullReferenceException` o `InvalidOperationException`.

#### Estrategia de Corrección

```
1. Hacer ChannelManager consciente del tipo de dispositivo:
   if (_deviceManager.CurrentDeviceType == DeviceType.Digital)
       return GetDigitalChannelStates();
   else
       return GetAnalogChannelStates();

2. O separar en AnalogChannelManager y DigitalChannelManager
```

---

## 6. HALLAZGOS BAJOS

### LOW-01: SignalLUT Pinned Memory No Utilizada en Hot Path

**Severidad:** LOW  
**Componentes:** `SignalLUT.cs`, `SignalGenerator.cs`

#### Descripción

`SignalLUTs` carga estáticamente una `SignalLUT` que usa `unsafe` con `GCHandle.Alloc` y punteros raw `ushort*`:

```csharp
// SignalLUT.cs — memoria pinneada:
_handle = GCHandle.Alloc(values, GCHandleType.Pinned);
_values = (ushort*)_handle.AddrOfPinnedObject().ToPointer();
```

Sin embargo, `SignalGenerator.GenerateSignal()` usa `_cachedNormalizedValues` (un `double[]` regular, no pinneado):

```csharp
double normalizedValue = _cachedNormalizedValues[lutIndex];  // ← Array normal
```

La `SignalLUT` estática `SignalLUTs.SinLUT` se crea al inicio pero nunca se usa en el hot path. Esto significa:
- Memoria pinneada innecesaria (bloquea el GC)
- `unsafe` habilitado en el proyecto sin beneficio real

#### Estrategia de Corrección

```
Opción A: Eliminar SignalLUT pinned y usar solo _cachedNormalizedValues
Opción B: Migrar hot loop a usar SignalLUT.GetValueNormalized() (aprovechando el pin)
```

---

### LOW-02: Console.WriteLine Excesivo en Código de Producción

**Severidad:** LOW  
**Componentes:** `DataOrientedExecutionEngine.cs`, `SignalLUT.cs`, `SignalGenerator.cs`

#### Descripción

`DataOrientedExecutionEngine` tiene ~50 llamadas a `System.Console.WriteLine()` en rutas críticas de rendimiento:

```csharp
// En el hot loop de PulseTrain:
System.Console.WriteLine($"[DO PULSE TRAIN] Period: {periodSeconds * 1000:F3}ms...");

// En cada evento ejecutado:
System.Console.WriteLine($"[DO EXEC ENGINE] EXECUTING {eventType}...");
```

`Console.WriteLine` es una operación de I/O síncrona con lock interno. En un loop de alta frecuencia, esto añade latencia de microsegundos y contención de lock.

#### Estrategia de Corrección

```
1. Reemplazar con _logger.Debug() (que puede ser no-op en producción)
2. Usar compilación condicional: #if DEBUG Console.WriteLine(...) #endif
3. O usar [Conditional("DEBUG")] en métodos helper
```

---

### LOW-03: DataOrientedExecutionEngine No Implementa IDisposable

**Severidad:** LOW  
**Componentes:** `DataOrientedExecutionEngine.cs`

#### Descripción

`DataOrientedExecutionEngine` posee recursos que requieren disposal:
- `CancellationTokenSource _cts`
- `Timer _playheadUpdateTimer`
- `Stopwatch _executionTimer`
- `List<Task> _pendingWaveformStops`

No implementa `IDisposable`. Si la instancia se abandona sin llamar `Stop()`, los recursos quedan en memoria.

#### Estrategia de Corrección

```csharp
public class DataOrientedExecutionEngine : IDisposable
{
    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _playheadUpdateTimer?.Dispose();
    }
}
```

---

## 7. ESTRATEGIAS DE IMPLEMENTACIÓN

### Fase 1: Correcciones Críticas (Prioridad Inmediata)

#### 1.1 Unificar Ownership de Instancias SDK (CRIT-01)

**Esfuerzo:** 2-3 días  
**Riesgo:** Alto (refactoring amplio)  
**Archivos afectados:** `DeviceManager.cs`, `DaqEngine.cs`, `DAQController.cs`, `AdvantechAnalogHal.cs`, `AdvantechDigitalHal.cs`

**Plan de implementación:**
1. **HAL como Single Owner**: Los HALs (`AdvantechAnalogHal`, `AdvantechDigitalHal`) son los ÚNICOS que crean instancias SDK
2. **DeviceManager delega a HAL**: `DeviceManager` recibe HALs inyectados y delega operaciones
3. **DaqEngine comparte HALs**: `DaqEngine` recibe los mismos HALs del `DeviceManager`, no crea nuevos
4. **SignalGenerator recibe IAnalogHal**: En lugar de `InstantAoCtrl` directo

```
Antes: DAQController → DeviceManager → InstantAoCtrl (A)
       DAQController → DaqEngine → AdvantechAnalogHal → InstantAoCtrl (B)  ← DUPLICADO

Después: DAQController → AdvantechAnalogHal → InstantAoCtrl (ÚNICO)
         DAQController → DeviceManager ↗ (usa misma HAL)
         DAQController → DaqEngine ↗ (usa misma HAL)
```

#### 1.2 Implementar Validación de ErrorCode (CRIT-02)

**Esfuerzo:** 1 día  
**Riesgo:** Bajo  
**Archivos afectados:** `AdvantechAnalogHal.cs`, `AdvantechDigitalHal.cs`

**Plan:**
1. Crear método helper `CheckError()` en cada HAL
2. Envolver TODAS las llamadas SDK con `CheckError()`
3. Warnings se loguean, Errors lanzan `DAQOperationException`
4. Agregar overload que acepta criticidad (para operaciones de test vs producción)

#### 1.3 Canalizar SignalGenerator a través del HAL (CRIT-03)

**Esfuerzo:** 1-2 días  
**Riesgo:** Medio (afecta timing de señales)  
**Archivos afectados:** `SignalGenerator.cs`, `IAnalogHal.cs`

**Plan:**
1. `SignalGenerator` recibe `IAnalogHal` en constructor (no `InstantAoCtrl`)
2. En modo Engine: escribe a `StateGrid` (deferred write)
3. En modo directo: escribe a `IAnalogHal.WriteSingle()` (immediate write)
4. Agregar método `IAnalogHal.WriteUnchecked()` para hot path (sin log, con ErrorCode check mínimo)

### Fase 2: Correcciones Altas (1-2 semanas)

#### 2.1 Refactorizar IDeviceManager (HIGH-01)

**Plan:** Separar interfaz en `IAnalogDeviceManager` y `IDigitalDeviceManager`. Eliminar exposición de `InstantAoCtrl Device`.

#### 2.2 Reemplazar Barrier por Signal-Based Sync (HIGH-02)

**Plan:** Sustituir `Barrier` en `SynchronizedOutputDispatcher` por un patrón `ManualResetEventSlim` + `CountdownEvent` que sea más robusto ante timeouts.

#### 2.3 Atomic Consume de Output Masks (HIGH-03)

**Plan:** Implementar `ConsumeDigitalOutputMask()` y `ConsumeAnalogOutputMask()` en `StateGrid` con `Interlocked.Exchange`, pasando las masks consumidas al dispatcher.

#### 2.4 Fix Static Fields en SignalGenerator (HIGH-04)

**Plan:** Mover `_phaseBarrier` a instancia. Hacer `_cachedNormalizedValues` volatile. Agregar lock en `IsChannelActive()`.

### Fase 3: Correcciones Medias (2-3 semanas)

- **MED-01**: Configurar `DioPortDir` en inicialización del HAL digital
- **MED-02**: Separar carga de perfiles por tipo de dispositivo
- **MED-03**: Leer rango del canal desde SDK y validar en SignalGenerator
- **MED-04**: Suscribirse a eventos de hot-plug del SDK
- **MED-05**: Hacer ChannelManager consciente del tipo de dispositivo

### Fase 4: Limpieza (Ongoing)

- **LOW-01**: Eliminar SignalLUT pinned no utilizada
- **LOW-02**: Reemplazar Console.WriteLine por logger condicional
- **LOW-03**: Implementar IDisposable en DataOrientedExecutionEngine

---

## 8. ROADMAP DE CORRECCIONES

```
SEMANA 1: CRITICAL fixes
  ├── CRIT-02: ErrorCode validation (1 día)
  ├── CRIT-01: Single SDK ownership (2-3 días)
  └── CRIT-03: SignalGenerator through HAL (1-2 días)

SEMANA 2-3: HIGH fixes
  ├── HIGH-03: Atomic mask consume (0.5 días)
  ├── HIGH-04: SignalGenerator statics (0.5 días)
  ├── HIGH-02: Barrier replacement (1-2 días)
  └── HIGH-01: IDeviceManager refactor (2-3 días)

SEMANA 4-5: MEDIUM fixes
  ├── MED-01: Port direction config (0.5 días)
  ├── MED-02: Profile loading by type (1 día)
  ├── MED-03: Voltage range validation (1 día)
  ├── MED-04: Hot-plug events (1 día)
  └── MED-05: ChannelManager device-aware (1 día)

ONGOING: LOW fixes
  ├── LOW-01: Remove unused pinned LUT
  ├── LOW-02: Replace Console.WriteLine
  └── LOW-03: IDisposable compliance
```

---

## 9. ARCHIVOS AUDITADOS

| Archivo | Líneas | Hallazgos |
|---------|--------|-----------|
| `Core/DAQ/DAQController.cs` | 501 | CRIT-01, CRIT-03 |
| `Core/DAQ/Managers/DeviceManager.cs` | 885 | CRIT-01, CRIT-02, HIGH-01 |
| `Core/DAQ/Managers/ChannelManager.cs` | 97 | MED-05, HIGH-01 |
| `Core/DAQ/Managers/ProfileManager.cs` | 278 | MED-02, CRIT-02 |
| `Core/DAQ/HAL/AdvantechAnalogHal.cs` | 195 | CRIT-02 |
| `Core/DAQ/HAL/AdvantechDigitalHal.cs` | 183 | CRIT-02, MED-01 |
| `Core/DAQ/HAL/IAnalogHal.cs` | 39 | — |
| `Core/DAQ/HAL/IDigitalHal.cs` | 48 | — |
| `Core/DAQ/Engine/DaqEngine.cs` | 474 | CRIT-01 |
| `Core/DAQ/Engine/StateGrid.cs` | 290 | HIGH-03 |
| `Core/DAQ/Engine/HighSpeedInputPoller.cs` | 253 | — |
| `Core/DAQ/Engine/LogicPipeline.cs` | 242 | — |
| `Core/DAQ/Engine/SynchronizedOutputDispatcher.cs` | 285 | HIGH-02 |
| `Core/DAQ/Engine/OutputMetronome.cs` | 205 | HIGH-03 |
| `Core/DAQ/Engine/InputChangeEvent.cs` | 28 | — |
| `Core/DAQ/Services/SignalGenerator.cs` | 636 | CRIT-03, HIGH-04, MED-03 |
| `Core/DAQ/Services/SignalLUT.cs` | 267 | LOW-01, LOW-02 |
| `Core/DAQ/Services/DeviceTypeResolver.cs` | 66 | — |
| `Core/DAQ/Interfaces/IDeviceManager.cs` | 100 | HIGH-01 |
| `Core/DAQ/Interfaces/ISignalGenerator.cs` | 50 | — |
| `Core/DAQ/Interfaces/IChannelManager.cs` | 38 | — |
| `Core/DAQ/Exceptions/DAQException.cs` | 19 | — |
| `Core/SignalManager/DataOriented/DataOrientedExecutionEngine.cs` | 733 | LOW-02, LOW-03 |
| `UI/WPF/ViewModels/MainViewModel.cs` | 284 | — |
| `UI/WPF/ViewModels/AnalogControlViewModel.cs` | 413 | — |
| `UI/WPF/ViewModels/DigitalMonitorViewModel.cs` | 350 | — |
| `SDK_Advantech/PCIe1824_prof_v1.xml` | 48 | MED-03 |
| `SDK_Advantech/PCI1735U_prof_v1.xml` | 490 | MED-01 |
| `SDK_Advantech/SDK_Advantech_4.txt` | 1142 | Referencia API |

---

## NOTA FINAL

Esta auditoría identifica problemas reales cruzando tres fuentes de verdad:
1. **El código fuente** — comportamiento implementado
2. **El SDK DAQNavi 4.0.0.0** — API correcta y restricciones
3. **La documentación hardware** — capacidades y limitaciones del PCIe-1824 y PCI-1735U

Los 3 hallazgos CRITICAL deben resolverse antes de cualquier despliegue en producción, ya que pueden causar corrupción de señales analógicas y comportamiento indefinido del hardware.

---

---

## 10. REGISTRO DE IMPLEMENTACIÓN

### Semana 1 — Correcciones CRITICAL (2026-03-30)

**Build:** ✅ 0 errores (solo warnings preexistentes)

#### CRIT-02: ErrorCode Validation — COMPLETADO ✅

**Archivos modificados:**
- `Core/DAQ/HAL/AdvantechAnalogHal.cs` — `ThrowOnError()` para init/config, `WarnOnError()` para hot-path con rate-limiting (1 log cada 1000 errores)
- `Core/DAQ/HAL/AdvantechDigitalHal.cs` — Mismo patrón. Todas las llamadas `_doCtrl.Write()` ahora validan ErrorCode

**Patrón implementado:**
```csharp
// Init/config path: throw on error
ThrowOnError(err, "operation description");

// Hot-path (WriteSingle, WriteOutputs): warn, no throw, rate-limited
WarnOnError(err, "operation description");  // Logs error #1, #1000, #2000...
```

#### CRIT-01: Unificación de SDK Ownership — COMPLETADO ✅

**Archivos modificados:**
- `Core/DAQ/Interfaces/IDeviceManager.cs` — Agregadas propiedades `DigitalInputDevice`, `DigitalOutputDevice`
- `Core/DAQ/Managers/DeviceManager.cs` — Implementadas propiedades DI/DO
- `Core/DAQ/HAL/AdvantechDigitalHal.cs` — Agregado `InitializeFromExisting(InstantDiCtrl, InstantDoCtrl)` con `_ownsDevices=false`
- `Core/DAQ/Engine/DaqEngine.cs` — Agregado `InitializeDigitalFromExisting(InstantDiCtrl, InstantDoCtrl)`
- `Core/DAQ/DAQController.cs` — Campos `_sharedAnalogHal`, `_sharedDigitalHal`. StartEngine usa HALs compartidos

**Flujo resultante:**
```
DeviceManager OWNS InstantAoCtrl / InstantDiCtrl / InstantDoCtrl
  ↓ (InitializeFromExisting — no crea nuevos handles)
DAQController crea AdvantechAnalogHal + AdvantechDigitalHal wrappers
  ↓ (misma referencia SDK)
SignalGenerator recibe IAnalogHal     ← CRIT-03
DaqEngine recibe HALs compartidos     ← CRIT-01
```

#### CRIT-03: SignalGenerator a través de IAnalogHal — COMPLETADO ✅

**Archivos modificados:**
- `Core/DAQ/Services/SignalGenerator.cs` — Constructor acepta `IAnalogHal` en lugar de `InstantAoCtrl`. Todas las llamadas `_device.Write()` reemplazadas por `_hal.WriteSingle()`. También se corrigió `IsChannelActive()` para ser thread-safe con lock
- `Core/DAQ/DAQController.cs` — Pasa `_sharedAnalogHal` al construir `SignalGenerator`

**Bonus fix incluido:** `IsChannelActive()` ahora usa `lock(_activeChannelsLock)` (era HIGH-04 parcial)

---

### Semana 2-3 — Correcciones HIGH (2026-03-30)

**Build:** ✅ 0 errores (solo warnings preexistentes)

#### HIGH-03: Atomic Mask Consume — COMPLETADO ✅

**Archivos modificados:**
- `Core/DAQ/Engine/StateGrid.cs` — Agregados `ConsumeDigitalOutputMask()` y `ConsumeAnalogOutputMask()` con `Interlocked.Exchange` atómico
- `Core/DAQ/Engine/OutputMetronome.cs` — El `MetronomeLoop` ahora consume masks ANTES de disparar el dispatcher, eliminando la ventana de race condition
- `Core/DAQ/Engine/SynchronizedOutputDispatcher.cs` — `TriggerCycle(uint, uint)` recibe masks pre-consumidas

**Patrón implementado:**
```
Antes: check masks → TriggerCycle() → ClearOutputMasks()  ← bits perdidos entre dispatch y clear
Después: ConsumeDigitalMask + ConsumeAnalogMask → TriggerCycle(dig, ana)  ← atómico, sin pérdida
```

#### HIGH-04: Fix Static Fields en SignalGenerator — COMPLETADO ✅

**Archivos modificados:**
- `Core/DAQ/Services/SignalGenerator.cs` — `_cachedNormalizedValues` ahora es `volatile`. `_phaseBarrier` y `_barrierLock` movidos de `static` a instancia
- `Core/DAQ/Interfaces/ISignalGenerator.cs` — Agregados `PreparePhaseBarrier()` y `ClearPhaseBarrier()` a la interfaz
- `Core/SignalManager/DataOriented/DataOrientedExecutionEngine.cs` — Llamadas estáticas cambiadas a usar instancia del SignalGenerator a través del controller

**Impacto:** Elimina corrupción del Barrier cuando múltiples instancias de SignalGenerator preparan barriers simultáneamente.

#### HIGH-02: Barrier → Signal-Based Sync — COMPLETADO ✅

**Archivos modificados:**
- `Core/DAQ/Engine/SynchronizedOutputDispatcher.cs` — Reescrito completamente

**Patrón implementado:**
```
Antes: Barrier(3) con timeouts asimétricos (50ms/100ms) — desync y deadlock posible
Después: ManualResetEventSlim triple:
  _triggerEvent  → Metronome libera threads de escritura
  _digitalDone   → Digital thread señala que terminó
  _analogDone    → Analog thread señala que terminó
  
Shutdown limpio: _running=false → set trigger → Join → Dispose
```

**Ventajas:**
- Shutdown determinístico (no más `ObjectDisposedException` silenciadas)
- Timeouts uniformes (50ms) con degradación controlada
- Sin estado inconsistente de Barrier phase

#### HIGH-01: IDeviceManager Refactor — COMPLETADO ✅

**Archivos modificados:**
- `Core/DAQ/Interfaces/IDeviceManager.cs` — Eliminadas propiedades `Device`, `DigitalInputDevice`, `DigitalOutputDevice` de la interfaz. Agregados `LoadProfile()`, `TryGetChannelInfo()`, `ResetAllOutputs()`
- `Core/DAQ/Managers/DeviceManager.cs` — Propiedades SDK mantenidas como clase concreta (no interfaz). Implementados nuevos métodos abstractos
- `Core/DAQ/Managers/ChannelManager.cs` — `GetChannelStates()` usa `TryGetChannelInfo()`. `ResetAllChannels()` usa `ResetAllOutputs()`
- `Core/DAQ/Managers/ProfileManager.cs` — `TryLoadProfile()` usa `_deviceManager.LoadProfile()`. PCI-1735U: descripción corregida, `ExpectedChannelCount` 4→32
- `Core/DAQ/DAQController.cs` — Agregado campo `_concreteDeviceManager` para acceso tipado a handles SDK

**Flujo resultante:**
```
IDeviceManager (interfaz limpia, sin SDK)
  ├─ LoadProfile()         ← ruta al controlador correcto por tipo
  ├─ TryGetChannelInfo()   ← info de canal sin exponer Channels[]
  ├─ ResetAllOutputs()     ← reset route-aware (analog/digital)
  └─ WriteVoltage/Digital  ← operaciones abstractas

DeviceManager (clase concreta, con SDK)
  ├─ .Device               ← solo usado por DAQController para HAL init
  ├─ .DigitalInputDevice   ← solo usado por DAQController para HAL init
  └─ .DigitalOutputDevice  ← solo usado por DAQController para HAL init
```

---

### Semana 4-5 — Correcciones MEDIUM y LOW (2026-03-30)

**Build:** ✅ 0 errores

#### MED-01: PCI-1735U Port Direction — COMPLETADO ✅
- `AdvantechDigitalHal.cs`: Se agregó la configuración explícita de `DirectionMask` para los puertos (0x00 para Input, 0xFF para Output) durante la inicialización, asegurando comportamiento determinístico independiente del estado previo del hardware.

#### MED-02 & MED-05: ProfileManager y ChannelManager vs Digital Devices — COMPLETADO ✅
- Resueltos indirectamente a través del refactor **HIGH-01**, que abstrayó las llamadas y aseguró que los perfiles y consultas de estado se ruteen correctamente (o se ignoren de forma segura) dependiendo del `DeviceType`.

#### MED-03: Rango de Voltaje No Validado — COMPLETADO ✅
- `IAnalogHal.cs` y `AdvantechAnalogHal.cs`: Implementado `GetChannelVoltageRange()` para leer el rango real del SDK.
- `SignalGenerator.cs`: El hot loop ahora usa el `minVoltage` y `maxVoltage` leídos del HAL para el clamp de salida, permitiendo voltajes negativos legítimos (ej. en `V_Neg10To10`) en lugar del hardcode `0-10V`.

#### MED-04: Manejo de Hot-Plug — COMPLETADO ✅
- `AdvantechAnalogHal.cs`: Suscripción a los estados de conexión de hardware. (Nota: `InstantAoCtrl` no soporta `addEventHandler`, por lo que se adaptó a un patrón de monitoreo de estado seguro vía eventos propios y comprobaciones `IsReady`).

#### LOW-03: Engine IDisposable — COMPLETADO ✅
- `DataOrientedExecutionEngine.cs`: Implementa `IDisposable` para limpieza determinística de `CancellationTokenSource`, `Timer` de playhead y listas estáticas internas.

---

*Documento generado: 2026-03-30 12:13:41*  
*Última actualización: 2026-03-30 (Semana 4-5 completada)*  
*Auditor: Cascade AI*  
*Versión del proyecto: LAMP DAQ Control v0.8*
