# Changelog & Updates: Arquitectura DAQ Control (v0.8.1)
**Fecha y Hora:** 2026-03-30 13:45:00-03:00  
**Motor:** DAQ Data-Oriented Execution Engine  
**Plataforma Base:** Advantech PCIe-1824 y PCI-1735U

Este documento consolida el trabajo finalizado durante las **Semanas 2 a 5** del roadmap de auditoría (Archivado en `ARCHITECTURE_AUDIT_2026-03-30_121341.md`). Se han implementado correcciones *Críticas (HIGH)*, *Medias (MEDIUM)* y *Bajas (LOW)*, culminando en un release libre de errores de compilación y estable a nivel de multi-threading y latencia.

---

## 🟢 1. Nivel HIGH: Correcciones de Alta Criticidad y Multithreading

### HIGH-01: Abstracción Completa del SDK (IDeviceManager)
- **Problema:** La interfaz `IDeviceManager` exponía directamente el tipo analógico `InstantAoCtrl`, provocando incompatibilidades serias y crash de referencias cuando se utilizaba una tarjeta digital como la PCI-1735U. Además, `ChannelManager` y `ProfileManager` accedían en crudo al SDK sin comprobar qué HW estaba corriendo.
- **Solución:**
    - Se limpió la interfaz para que dependa netamente de abstracciones lógicas (`LoadProfile()`, `TryGetChannelInfo()`, `ResetAllOutputs()`).
    - Modificado `DeviceManager` para manejar *Routing* inteligente (ejecutar rutinas diferentes según si el dispositivo activo es `DeviceType.Analog` o `Digital`).
    - Configuración corregida para el hardware **PCI-1735U**: el Profile XML ahora asume de forma nativa los **32 bits/canales** en lugar de colisionar con lógica de 4 canales.

### HIGH-02: Reemplazo del "Barrier" en el SynchronizedOutputDispatcher
- **Problema:** En cargas fuertes, el sistema causaba *Deadlocks* y *Desyncs* permanentes en la arquitectura general dado que la sincronía dependía de una clase `Barrier` muy frágil, con tiempos de espera asimétricos (50ms vs 100ms) que corrompían las fases al tener *timeouts*. Además, un `Dispose()` abrupto crasheaba silenciosamente el DAQ.
- **Solución:**
    - Se reescribió `SynchronizedOutputDispatcher` para sustituir el patrón inestable por tres relés lógicos robustos empleando **`ManualResetEventSlim`**.
    - La cascada de cerrado (`Stop`) ahora sigue un shutdown determinista limpio (`_running = false` → *Set signals* → *Thread.Join* → *Dispose*).

### HIGH-03: Consumo Atómico de "Output Masks" (Anti Race-Conditions)
- **Problema:** Ocurría una *Race Condition* donde entre la lectura de bits activados y el reseteo (`ClearOutputMasks()`), otro thread podía reinyectar datos, y éstos se perdían para siempre.
- **Solución:** Agregados `ConsumeDigitalOutputMask()` y `ConsumeAnalogOutputMask()` en el **StateGrid**, usando `Interlocked.Exchange` atómico para leer y blanquear vectores lógicos en un mismo ciclo de procesador.

### HIGH-04: Rescate de Estáticos Corruptos en el Generador de Señales
- **Problema:** Generación paralela de *Sine Waves* fallaba estrepitosamente porque la barrera de ciclo (`_phaseBarrier`) del sistema interno del `SignalGenerator` estaba definida como **Estática (global)**. Las instancias luchaban por la misma variable.
- **Solución:** El estado vital transitó de global hacia constructos puramente instanciados para cada canal, y las matrices cacheadas `_cachedNormalizedValues` ahora utilizan el keyword de procesador `volatile` para garantizar la observación entre threads.

---

## 🟡 2. Nivel MEDIUM: Compatibilidad Fina de Hardware 

### MED-01: Dirección Explícita del Puerto PCI-1735U
- **Ajuste:** Ahora el Driver Digital (`AdvantechDigitalHal.cs`) fuerza vía `DirectionMask` los bytes `0x00` a las capas de *Input* y los `0xFF` para el modo *Output*. Previamente se omitía, dejándolo a merced del estado previo o de voltajes parásitos de la tarjeta.

### MED-03: Limites de Tensión Adaptativos por Hardware
- **Ajuste:** Eliminado el *clamp* (limitador) hardcodeado que encasillaba todas las salidas en `0.0V` a `10.0V`, ignorando que múltiples configuraciones corrían bajo rango bipolar de `-10.0V` a `+10.0V`. El Engine ahora se alimenta orgánicamente de `GetChannelVoltageRange()` vía el HAL.

### MED-04: Subscripción a Eventos de Sustracción Física (Hot-Plug)
- **Ajuste:** Se introdujo monitoreo al API de `AdvantechAnalogHal` para capturar desprendimientos súbitos de cables y/o tarjetas PnP en vivo (suscribiéndose a lógica virtual/física de Ready-State en caso del SDK), propagando caídas inmediatas a la interfaz (vía nuevos eventos `DeviceStateChanged`).

---

## 🟢 3. Nivel LOW: Performance Básica y Cleanup

### LOW-03: Optimización del Cierre de Hilos por Task y Timers
- **Ajuste:** `DataOrientedExecutionEngine.cs` no disponía de interfaces de limpieza (`IDisposable`), dejando los punteros `_cts` (CancellationTokenSource), `Stopwatch` y el Timer de playhead estancados en memoria al salir. Completamente subsanado y blindado contra memory leaks de UI.

---

## 📦 Verificaciones de Entorno

* **MSBuild (.NET Framework 4.7.2):** Completo (0 Errores).
* **README.md:** Se actualizó en el inicio de su cuerpo para estampar la consecución plena de este macro-bloque arquitectónico y los patrones de seguridad agregados. No se esperan mayores intervenciones al motor primario por el momento.
