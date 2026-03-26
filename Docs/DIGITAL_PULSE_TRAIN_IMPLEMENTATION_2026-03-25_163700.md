# Digital Pulse Train Implementation
**Fecha de creación**: 2026-03-25 16:37:00  
**Estado**: En desarrollo  
**Objetivo**: Implementar entidad DigitalPulseTrain para generación de trenes de pulsos TTL

---

## 📋 Especificaciones Técnicas

### Tipo de Señal
- **Tren de pulsos TTL** (Transistor-Transistor Logic)

### Niveles Lógicos (Amplitud)
- **V_OL (Low)**: 0 Vdc (fijo)
- **V_OH (High)**: Configurable por usuario
  - 3.3 Vdc
  - 5.0 Vdc

### Control de Tiempo
- **Frecuencia (f)**: Hz - Tasa de repetición de pulsos
- **Duty Cycle (D)**: 1% - 100% - Porcentaje del periodo en estado alto

### Cálculos Automáticos
```
T = 1/f                    (Periodo total)
t_on = D · T               (Tiempo en estado HIGH)
t_off = (1 - D) · T        (Tiempo en estado LOW)
```

---

## 🎯 Fase 1: Análisis de Arquitectura Existente

### ✅ Estado: COMPLETADO

**Estructura de la librería:**
- Clase base: `SignalEvent` (`Core/SignalManager/Models/SignalEvent.cs`)
- Enum de tipos: `SignalEventType` (`Core/SignalManager/Models/SignalEventType.cs`)
- Librería: `SignalLibrary` (`Core/SignalManager/Services/SignalLibrary.cs`)
- Serialización: JSON (Newtonsoft.Json)

**SignalEventTypes existentes:**
- `DC` - Voltaje DC constante (analógico)
- `Ramp` - Rampa de voltaje (analógico)
- `Waveform` - Forma de onda senoidal (analógico)
- `DigitalPulse` - Pulso digital simple (digital)
- `DigitalState` - Estado digital HIGH/LOW (digital)
- `Wait` - Espera/delay

**Parámetros en Dictionary<string, double>:**
- Señales analógicas: `voltage`, `frequency`, `amplitude`, `offset`, `startVoltage`, `endVoltage`
- Señales digitales: `state`, `read`, `trigger_state`

**Categorías existentes:**
- "Digital Write"
- "Digital Read"
- "Analog DC"
- "Analog Ramps"
- "Analog Waveforms"

---

## 🎯 Fase 2: Diseño de DigitalPulseTrain

### ✅ Estado: COMPLETADO

**Decisión de arquitectura:**
- ✅ **IMPLEMENTADO**: Creado nuevo `SignalEventType.PulseTrain` 
- ✅ Diferenciación clara entre pulsos simples (`DigitalPulse`) y trenes continuos (`PulseTrain`)
- ✅ Semántica explícita para mejor mantenibilidad

**Parámetros implementados:**
```csharp
Parameters = {
    { "frequency", f },           // Hz (validado > 0)
    { "dutyCycle", D },           // 0.01 - 1.00 (1% - 100%)
    { "vHigh", V_OH },            // 2.0 - 5.5 Vdc (típico: 3.3 o 5.0)
    { "vLow", 0.0 }               // Siempre 0 Vdc
}
```

**Cálculos derivados (calculados en tiempo de ejecución por ExecutionEngine):**
- `period = 1.0 / frequency`
- `tOn = dutyCycle * period`
- `tOff = (1.0 - dutyCycle) * period`

**Nueva categoría creada:**
- "Digital Pulse Trains"

---

## 🎯 Fase 3: Implementación

### ✅ Estado: COMPLETADO (2026-03-25 16:38)

**Archivos modificados:**

1. **`SignalEventType.cs`** - Nuevo tipo agregado
   - Línea 33-36: `SignalEventType.PulseTrain`

2. **`SignalLibrary.cs`** - Método y señales predefinidas
   - Líneas 219-258: Método `CreateDigitalPulseTrain()`
   - Líneas 43-49: 6 señales predefinidas en categoría "Digital Pulse Trains"

3. **`SignalEvent.cs`** - Validación completa
   - Líneas 265-301: Validación de `PulseTrain` con:
     - `frequency > 0`
     - `dutyCycle` entre 0.01 y 1.00
     - `vHigh` entre 2.0V y 5.5V (TTL compatible)

**Señales predefinidas implementadas:**
```
✅ PulseTrain 1kHz @ 50% (3.3V)
✅ PulseTrain 1kHz @ 50% (5V)
✅ PulseTrain 10kHz @ 25% (3.3V)
✅ PulseTrain 10kHz @ 75% (5V)
✅ PulseTrain 100kHz @ 10% (5V)
✅ PulseTrain 100kHz @ 90% (3.3V)
```

---

## 🎯 Fase 4: Integración con ExecutionEngine

### ⏳ Estado: PENDIENTE

**Modificaciones necesarias:**
- [ ] Actualizar `ExecutionEngine.ExecuteEvent()` para manejar PulseTrain
- [ ] Implementar generación de señal en hardware (DAQController)
- [ ] Considerar si requiere threading dedicado para alta frecuencia

**Consideraciones de rendimiento:**
- Frecuencias bajas (<1kHz): Implementación por software con `Task.Delay()`
- Frecuencias altas (>1kHz): Requiere hardware timer o streaming de DAQ

---

## 🎯 Fase 5: Testing y Validación

### ⏳ Estado: PENDIENTE

**Tests unitarios requeridos:**
- [ ] Validación de parámetros (frequency > 0, 0.01 ≤ dutyCycle ≤ 1.0)
- [ ] Validación de V_OH (solo 3.3 o 5.0 Vdc)
- [ ] Cálculo correcto de t_on y t_off
- [ ] Serialización/deserialización JSON
- [ ] Integración con SignalLibrary

---

## 📊 Métricas de Progreso

| Fase | Estado | Progreso | Fecha Completada |
|------|--------|----------|------------------|
| 1. Análisis | ✅ Completado | 100% | 2026-03-25 16:37 |
| 2. Diseño | ✅ Completado | 100% | 2026-03-25 16:38 |
| 3. Implementación | ✅ Completado | 100% | 2026-03-25 16:38 |
| 4. Integración | ⏳ Pendiente | 0% | - |
| 5. Testing | ⏳ Pendiente | 0% | - |

**Progreso total**: 60% (3/5 fases)

---

## 🔧 Decisiones Técnicas Implementadas

1. **✅ Crear nuevo SignalEventType.PulseTrain**
   - **IMPLEMENTADO**: Se creó nuevo tipo `PulseTrain`
   - Claridad semántica: Diferencia explícita entre pulsos simples y trenes continuos
   - Mantenibilidad: Código más legible y extensible

2. **✅ Validación flexible de V_OH**
   - **IMPLEMENTADO**: Rango 2.0V - 5.5V
   - Valores típicos recomendados: 3.3V y 5.0V
   - Balance entre cumplimiento TTL y flexibilidad práctica
   - Mensajes de validación informativos

3. **⏳ Implementación en hardware/software (PENDIENTE)**
   - Requiere verificación de capacidades de PCI-1735U
   - Debe implementarse en ExecutionEngine (Fase 4)
   - Opciones:
     - Hardware PWM (preferido si disponible)
     - Software timing con Task.Delay() (fallback)

---

## 📝 Notas de Desarrollo

### 2026-03-25 16:37
- Análisis de arquitectura completado
- SignalEvent usa patrón flexible con Dictionary<string, double>
- Sistema de categorías permite organización clara
- JSON serialization ya implementada y funcional

### 2026-03-25 16:38 - IMPLEMENTACIÓN COMPLETADA
**Archivos modificados:**
1. `Core/SignalManager/Models/SignalEventType.cs` (+4 líneas)
2. `Core/SignalManager/Services/SignalLibrary.cs` (+46 líneas)
3. `Core/SignalManager/Models/SignalEvent.cs` (+37 líneas)

**Funcionalidades agregadas:**
- ✅ Nuevo tipo `SignalEventType.PulseTrain`
- ✅ Método `CreateDigitalPulseTrain()` con documentación completa
- ✅ 6 señales predefinidas en categoría "Digital Pulse Trains"
- ✅ Validación robusta de parámetros:
  - Frequency > 0 Hz
  - Duty cycle: 0.01 - 1.00 (1% - 100%)
  - V_OH: 2.0V - 5.5V (TTL compatible)
  - V_OL: 0V (fijo)

**Cálculos implementados (en parámetros):**
```csharp
T = 1/f                    // Periodo (calculado por ExecutionEngine)
t_on = D · T               // Tiempo HIGH (calculado por ExecutionEngine)
t_off = (1 - D) · T        // Tiempo LOW (calculado por ExecutionEngine)
```

**Próximos pasos (Fase 4):**
- Implementar ejecución de PulseTrain en `ExecutionEngine.cs`
- Definir estrategia hardware vs software según PCI-1735U
- Considerar threading para alta frecuencia (>1kHz)

---

## 🔗 Referencias

**Archivos relacionados:**
- `@c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\Core\SignalManager\Models\SignalEvent.cs`
- `@c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\Core\SignalManager\Models\SignalEventType.cs`
- `@c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\Core\SignalManager\Services\SignalLibrary.cs`
- `@c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\Core\SignalManager\Services\ExecutionEngine.cs`

**Documentación:**
- Especificación TTL: V_OH = 2.4V min (típico 3.3V o 5.0V)
- PWM Duty Cycle: D = t_on / T
