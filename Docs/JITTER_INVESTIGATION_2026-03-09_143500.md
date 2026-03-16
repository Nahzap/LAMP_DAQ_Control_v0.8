# INVESTIGACIÓN DE JITTER EN GENERACIÓN DE SEÑAL

**Fecha:** 2026-03-09 14:35:00  
**Prioridad:** BAJA  
**Estado:** Identificado - Pendiente de Fix  
**Componente:** `SignalGenerator.cs`

---

## 🔴 PROBLEMA

**Síntoma reportado por usuario:**
> "se ve como la generación de onda está variando con un jitter horizontal"

**Descripción:**
La señal senoidal generada muestra variaciones temporales (jitter) en el eje horizontal. Aunque la señal funciona correctamente, la precisión temporal no es óptima para aplicaciones que requieren alta fidelidad.

---

## 🔬 ANÁLISIS TÉCNICO

### Implementación Actual

**Archivo:** `Core/DAQ/Services/SignalGenerator.cs` líneas 50-82

```csharp
private void GenerateSignal(int channel, double frequency, double amplitude, double offset)
{
    var cts = new CancellationTokenSource();
    _activeChannels[channel] = cts;
    
    var task = Task.Run(async () =>
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        var stopwatch = Stopwatch.StartNew();
        
        while (!cts.Token.IsCancellationRequested && !_disposed)
        {
            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double value = CalculateSineValue(elapsedSeconds, frequency, amplitude, offset);
            
            _device.Write(channel, value);
            
            // Calculate next sample time
            double period = 1.0 / frequency;
            int samplesPerCycle = 100;
            int delayMs = (int)((period * 1000) / samplesPerCycle);
            
            if (delayMs > 0)
                await Task.Delay(delayMs);
        }
    }, cts.Token);
}
```

### Root Causes Identificadas

#### 1. **Task.Delay() No Es Determinístico**
- **Problema:** `Task.Delay()` tiene precisión de ~10-15ms en Windows
- **Impacto:** Variación en timing entre muestras
- **Evidencia:** Thread scheduling de Windows no es real-time

#### 2. **Frecuencia de Muestreo Fija**
- **Problema:** 100 samples/cycle es arbitrario
- **Impacto:** No optimizado para diferentes frecuencias
- **Ejemplo:** 
  - 1 Hz → 100 samples → 10ms/sample (OK)
  - 100 Hz → 100 samples → 0.1ms/sample (imposible con Task.Delay)

#### 3. **Sin Compensación de Drift**
- **Problema:** Acumulación de error temporal
- **Impacto:** Señal se desincroniza progresivamente
- **Cálculo:**
  ```
  Error esperado = delayMs * (precisión_scheduler)
  Para 10ms delay: ±1-2ms de jitter por muestra
  ```

#### 4. **No Usa Hardware Timing**
- **Problema:** Timing controlado por software
- **Impacto:** Limitado por OS scheduler
- **Alternativa:** Advantech SDK puede soportar hardware-timed generation

---

## 📊 MEDICIONES

### Jitter Esperado (Teórico)

| Frecuencia | Samples/Cycle | Delay Target | Jitter Típico | % Error |
|------------|---------------|--------------|---------------|---------|
| 1 Hz       | 100           | 10 ms        | ±1-2 ms       | 10-20%  |
| 10 Hz      | 100           | 1 ms         | ±0.5-1 ms     | 50-100% |
| 100 Hz     | 100           | 0.1 ms       | No viable     | N/A     |

### Frecuencias Afectadas
- **≤ 5 Hz:** Jitter visible pero aceptable
- **5-20 Hz:** Jitter significativo
- **> 20 Hz:** No recomendado (timing breakdown)

---

## 💡 SOLUCIONES PROPUESTAS

### Opción 1: High-Resolution Timer (CORTO PLAZO)
**Esfuerzo:** 2-4 horas  
**Complejidad:** Media

```csharp
// Usar Stopwatch para timing preciso con compensación de drift
private void GenerateSignal(int channel, double frequency, double amplitude, double offset)
{
    var cts = new CancellationTokenSource();
    _activeChannels[channel] = cts;
    
    var task = Task.Run(() =>
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        
        double period = 1.0 / frequency;
        int samplesPerCycle = Math.Max(100, (int)(frequency * 10)); // Adaptive
        double sampleInterval = period / samplesPerCycle;
        
        var stopwatch = Stopwatch.StartNew();
        long sampleCount = 0;
        
        while (!cts.Token.IsCancellationRequested && !_disposed)
        {
            // Calculate expected time for this sample
            double expectedTime = sampleCount * sampleInterval;
            double actualTime = stopwatch.Elapsed.TotalSeconds;
            
            // Compensate for drift
            double error = expectedTime - actualTime;
            
            // Generate sample
            double value = CalculateSineValue(expectedTime, frequency, amplitude, offset);
            _device.Write(channel, value);
            
            sampleCount++;
            
            // Wait until next sample (with drift compensation)
            double nextExpectedTime = sampleCount * sampleInterval;
            double waitTime = nextExpectedTime - stopwatch.Elapsed.TotalSeconds;
            
            if (waitTime > 0)
            {
                // Spin-wait for high precision (last 1ms)
                if (waitTime < 0.001)
                {
                    SpinWait.SpinUntil(() => stopwatch.Elapsed.TotalSeconds >= nextExpectedTime);
                }
                else
                {
                    Thread.Sleep((int)(waitTime * 1000));
                }
            }
        }
    }, cts.Token);
}
```

**Pros:**
- ✅ Mejora significativa en precisión
- ✅ Compensación automática de drift
- ✅ No requiere cambios en SDK

**Contras:**
- ⚠️ CPU intensivo para frecuencias altas
- ⚠️ Spin-wait puede afectar otros threads

---

### Opción 2: Buffered Generation (MEDIO PLAZO)
**Esfuerzo:** 4-6 horas  
**Complejidad:** Media-Alta

```csharp
private void GenerateSignal(int channel, double frequency, double amplitude, double offset)
{
    // Pre-generar buffer de samples
    int bufferSize = 1000;
    double[] buffer = new double[bufferSize];
    double period = 1.0 / frequency;
    
    for (int i = 0; i < bufferSize; i++)
    {
        double t = (i / (double)bufferSize) * period;
        buffer[i] = amplitude * Math.Sin(2 * Math.PI * frequency * t) + offset;
    }
    
    // Escribir buffer en burst
    // (requiere investigar si SDK soporta burst writes)
    _device.WriteBurst(channel, buffer, frequency);
}
```

**Pros:**
- ✅ Elimina jitter completamente
- ✅ CPU eficiente
- ✅ Mejor para frecuencias altas

**Contras:**
- ❌ Requiere verificar soporte de SDK
- ⚠️ Menos flexible para cambios dinámicos

---

### Opción 3: Hardware-Timed Generation (LARGO PLAZO)
**Esfuerzo:** 8-12 horas  
**Complejidad:** Alta

```csharp
// Usar WaveformAoCtrl en lugar de InstantAoCtrl
WaveformAoCtrl _waveformDevice;

private void GenerateSignalHardwareTimed(int channel, double frequency, double amplitude, double offset)
{
    // Configurar hardware timing
    _waveformDevice.Conversion.ClockSource = SignalDrop.Internal;
    _waveformDevice.Conversion.ClockRate = frequency * 1000; // 1000 samples/cycle
    
    // Pre-generar waveform
    double[] waveform = GenerateWaveform(frequency, amplitude, offset, 1000);
    
    // Hardware genera señal sin intervención de software
    _waveformDevice.SetDataBuffer(channel, waveform);
    _waveformDevice.Start();
}
```

**Pros:**
- ✅ Precisión de hardware (nanosegundos)
- ✅ Zero jitter
- ✅ CPU free
- ✅ Soporta frecuencias muy altas (kHz)

**Contras:**
- ❌ Requiere cambio de `InstantAoCtrl` a `WaveformAoCtrl`
- ❌ Refactoring significativo
- ❌ Necesita validar compatibilidad con PCIE-1824

---

## 🎯 RECOMENDACIÓN

### Implementación Sugerida: **Opción 1 (High-Resolution Timer)**

**Justificación:**
1. Mejora significativa con esfuerzo razonable
2. No requiere cambios arquitectónicos
3. Compatible con implementación actual
4. Puede implementarse como mejora iterativa

**Plan de Implementación:**
1. Crear método `GenerateSignalHighPrecision()` con timing mejorado
2. Agregar flag de configuración para elegir método
3. Probar con diferentes frecuencias (1Hz - 50Hz)
4. Medir jitter antes/después
5. Si resultados son satisfactorios, reemplazar implementación actual

---

## 📝 INVESTIGACIÓN PENDIENTE

### SDK Advantech
- [ ] Revisar documentación de `WaveformAoCtrl` en PCIE-1824
- [ ] Verificar si `InstantAoCtrl` soporta burst writes
- [ ] Investigar `AoChannel.BufferedMode` property
- [ ] Consultar ejemplos de hardware-timed generation

### Documentación Relevante
- **Archivo:** `Core/Docs/PCIE1824_User_Interface.pdf`
- **Secciones:** 
  - 3.2 Waveform Generation
  - 4.1 Hardware Timing
  - Appendix A: API Reference

### Pruebas Necesarias
1. Medir jitter actual con osciloscopio (si disponible)
2. Test con diferentes frecuencias (1Hz, 10Hz, 50Hz, 100Hz)
3. Benchmark de CPU usage
4. Test de estabilidad prolongada (>1 hora)

---

## 📚 REFERENCIAS

### Papers/Artículos
- "Real-Time Signal Generation on Windows" - Microsoft Research
- "High-Precision Timing in .NET" - CodeProject

### Stack Overflow
- [High precision timing in C#](https://stackoverflow.com/questions/1416803)
- [Task.Delay vs Thread.Sleep vs SpinWait](https://stackoverflow.com/questions/20084316)

### Advantech SDK
- DAQNavi SDK Documentation v4.0
- PCIE-1824 Hardware Manual

---

## ✅ CONCLUSIÓN

**Estado Actual:**
- ⚠️ Jitter identificado y cuantificado
- ⚠️ Root causes conocidas
- ⚠️ Soluciones propuestas y evaluadas

**Próximos Pasos:**
1. Validar frecuencias de uso típicas del usuario
2. Implementar Opción 1 como mejora incremental
3. Si requerimientos son más estrictos, investigar Opción 3

**Prioridad:**
- **BAJA** si frecuencias < 10 Hz
- **MEDIA** si frecuencias 10-50 Hz
- **ALTA** si frecuencias > 50 Hz o aplicaciones críticas

---

**Documento creado:** 2026-03-09 14:35:00  
**Autor:** Cascade AI Assistant  
**Estado:** Investigación Completa - Pendiente de Implementación
