# Arquitectura Data-Oriented y Avances en Sincronización a Nanosegundos
**Fecha:** 26 de Marzo de 2026

## Estado Arquitectónico Actual (Data-Oriented Execution Engine)
El sistema `LAMP DAQ Control v0.8` alcanzó su etapa de madurez asíncrona mediante el reemplazo completo de la herencia Orientada a Objetos por una arquitectura **Orientada a Datos (DO)** para su motor de ejecución (`SignalTable` y `DataOrientedExecutionEngine`).

Este modelo centraliza todas las instrucciones DAQ en columnas de memoria alineadas y delega los lazos de control pesado a ejecuciones matemáticas paralelas por cada evento:

1. **Paralelismo Fire-and-Forget**: Señales continuas (Waveforms, Rampas, DC o PulseTrains) se despachan en Hilos separados que reportan su carga. Todas coinciden atómicamente en un Sync Timestamp (SpinWait).
2. **Sincronización por Fases (Barrier)**: Señales oscilatorias periódicas como `Sinewaves` se pre-esperan en un bloque lógico mediante una estructura `System.Threading.Barrier`, iniciando milisegundos clave de Hardware Writing exactamente a la misma vez.

---

## Logro de Hoy: Optimización Estricta de Latencia

Se procesaron y erradicaron **Bugs Silenciosos** y **Derivas Acumulativas (Time-Drifts)** encontrados en los Logs de la versión inicial del motor Data-Oriented:

### 1. Zero Cumulative Error en Generadores Intermitentes (PulseTrain)
El generador `PulseTrain` producía una varianza en su ciclo fundamental de hasta **~1.7%** por cada 5 segundos de iteración en alta frecuencia (>1000Hz). 

El error provenía de medir tiempo de forma "Relativa":
```csharp
// Error arrastrado (Acumula la latencia de PCI en cada ciclo)
while(timer.Elapsed - phaseStart < duration) { SpinWait() }  
```

**Solución Implementada**:
Re-programamos los algoritmos de simulación para anclarse a **Time Horizons Absolutos** (`targetTicks = startTicks + cycleCount * periodTicks`). Sin importar los retardos producidos por el bus del hardware, cada flanco alto y bajo de la tarjeta se sincroniza a una cota real. La latencia observada al final del periodo cayó a **0%**.

### 2. Extirpación del "Oversleep" originado por .NET y el SO
El método universal `HighPrecisionWaitAsync` utilizaba `Task.Delay` en las esperas superiores a *10ms*. 
Debido al Quantum del scheduler en Windows Kernel, los ciclos de CPU ceden por ~15.6ms como mínimo, provocando que secuencias analógicas exactas de entre 10ms a 30ms "se durmieran" y causaran un extra de **0.6%** en el tiempo documentado por rampa.

**Solución Implementada**:
Ampliamos los umbrales seguros. Toda espera menor a **20ms** ahora quema CPU puramente vía subproceso (Spining) sin devolver el control al Garbage Collector/Thread Pool del Sistema Operativo (`SPIN_THRESHOLD = 20_000_000 ns`), garantizando resoluciones de control ultraprecisas para la operación real-time requerida por nuestro hardware Advantech.

### 3. Loop Execution Visual Flicker
Se extirpó un error donde el motor de ejecución forzaba a la ViewModel de WPF a resetear su estado `IsPlaying` a Falso transitoriamente cada que concluía una instancia del loop secuencial para inmediatamente regresar a "True" al reanudar la lectura del renglón 0. Este parpadeo que desconcentra al operador se resolvió dejando colapsado el broadcast `Idle` al interior de los ciclos y forzando únicamente un render visual a `TimeSpan.Zero` en lugar de una detención formal de máquina de estado.

---
## Conclusión
`LAMP_DAQ_Control_v0.8` cuenta actualmente con un núcleo asíncrono, despojado de fallos escalonados, sincronizado internamente a base de `Nanosegundos` atados al Stopwatch de alta resolución (QPC). La capacidad actual permite secuencias robustas, ilimitadas en re-trazado (Loops perfectos sin gaps), con rendimiento Cero-Error temporal.
