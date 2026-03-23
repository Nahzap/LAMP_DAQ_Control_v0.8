# Plan de Corrección: Precisión Temporal y Nomenclatura de Señales
**Fecha:** 2026-03-23 11:52  
**Autor:** Sistema de Desarrollo Cascade  
**Estado:** 🔍 PLAN DE INVESTIGACIÓN - Pendiente de Aprobación

---

## 🎯 Objetivo
Corregir dos defectos críticos en el Signal Manager que impiden el uso profesional del sistema de control DAQ:
1. **Precisión temporal**: No se pueden ingresar valores decimales ni seleccionar unidades sub-segundo (ms, µs, ns)
2. **Nomenclatura de señales**: Los nombres no se actualizan en el grid/timeline y no son editables correctamente

---

## 🔬 Análisis de Causa Raíz

### PROBLEMA 1: Restricción de Precisión Temporal

#### Síntoma
El usuario no puede ingresar valores como `0.75` segundos, `750` ms ni `750000` µs. Los campos de tiempo se comportan como si solo aceptaran enteros.

#### Archivos Investigados
| Archivo | Líneas Clave | Hallazgo |
|---------|-------------|----------|
| `SignalManagerView.xaml` | 206-212 | Bindings `SelectedEventStartSeconds` y `SelectedEventDurationMs` sin `StringFormat` |
| `SignalManagerViewModel.cs` | 227-250 | Properties son `double` pero WPF no las trata correctamente |
| `SignalEvent.cs` | 25-30 | `TimeSpan` StartTime/Duration (precisión inherente: hasta ticks = 100ns) |
| `SignalTable.cs` | 19-20 | `long[] StartTimesNs/DurationsNs` (precisión nanosegundos ✓) |

#### Causa Raíz (3 componentes)

**CR-1.1: Bug clásico de WPF con `UpdateSourceTrigger=PropertyChanged` en `double`**
```xml
<!-- ACTUAL (línea 207-208 de SignalManagerView.xaml) -->
<TextBox Text="{Binding SelectedEventStartSeconds, UpdateSourceTrigger=PropertyChanged}" />
```
Cuando el usuario escribe `0.` (punto decimal), WPF intenta parsear inmediatamente → falla o trunca a `0` → elimina el punto decimal. Es un bug conocido de WPF cuando se usa `UpdateSourceTrigger=PropertyChanged` con tipos numéricos.

**CR-1.2: Sin selector de unidades temporales**
Los labels están hardcodeados: `"Start Time (s):"` y `"Duration (ms):"`. No hay forma de cambiar entre s/ms/µs/ns. Para señales eléctricas reales se necesitan rangos desde nanosegundos hasta segundos.

**CR-1.3: Sin `StringFormat` en bindings numéricos**
Los TextBox no especifican `StringFormat` para mostrar decimales correctamente, causando pérdida de precisión visual.

#### Impacto
- **Imposible** configurar duraciones sub-segundo con precisión (ej: 0.75s, 250ms, 100µs)
- **Imposible** configurar start times con precisión fraccionaria
- **Crítico** para control de señales eléctricas reales donde la temporización precisa es fundamental

---

### PROBLEMA 2: Nombres de Señales No Editables / No Auto-actualizables

#### Síntoma
1. En la vista "Events List" (DataGrid), las columnas de nombre no son editables
2. En el panel "Event Details", cambiar el Name y hacer "Apply Changes" no actualiza el nombre en el DO table
3. Los nombres no se auto-generan según las características (ej: al cambiar frecuencia de 500Hz a 1kHz, debería cambiar "Sine 500Hz" → "Sine 1kHz")
4. En la timeline (Figura 4), los bloques muestran el nombre original sin actualizar

#### Archivos Investigados
| Archivo | Líneas Clave | Hallazgo |
|---------|-------------|----------|
| `SignalManagerView.xaml` | 195-196 | Binding a `SelectedEvent.Name` - SignalEvent NO implementa INotifyPropertyChanged |
| `SignalManagerView.xaml` | 289-302 | DataGrid: columnas `DataGridTextColumn` sin IsReadOnly=False explícito, binding Mode=OneWay implícito |
| `SignalManagerViewModel.cs` | 743-826 | `OnApplyEventChanges()` - **NO actualiza `table.Names[i]`** |
| `SignalEvent.cs` | 10-73 | Clase POCO sin INotifyPropertyChanged |
| `TimelineControl.xaml` | 168 | `Text="{Binding SignalEvent.Name}"` - binding estático sin notificación |
| `TimelineChannelViewModel.cs` | 199-203 | `UpdateDisplayText()` solo se llama en constructor, no al cambiar Name |
| `SignalTable.cs` | 221-230 | `UpdateNameAndColor()` existe pero NO es llamado desde `OnApplyEventChanges` |

#### Causa Raíz (3 componentes)

**CR-2.1: `OnApplyEventChanges` NO sincroniza el nombre con el DO table**
```csharp
// ACTUAL (líneas 783-805 de SignalManagerViewModel.cs)
// Se actualizan StartTimesNs, DurationsNs, voltajes, waveform params
// PERO FALTA:
table.Names[i] = SelectedEvent.Name;        // ← NO EXISTE
table.Colors[i] = SelectedEvent.Color;       // ← NO EXISTE
```
El método `UpdateSignal` del `DataOrientedSequenceManager` SÍ llama `UpdateNameAndColor`, pero `OnApplyEventChanges` modifica el table directamente sin pasar por el adapter/manager, omitiendo la actualización del nombre.

**CR-2.2: `SignalEvent` no implementa `INotifyPropertyChanged`**
```csharp
// ACTUAL (SignalEvent.cs línea 20)
public string Name { get; set; }  // Simple auto-property, sin notificación
```
Al cambiar `Name` en el TextBox del panel Event Details, el DataGrid y Timeline no se enteran porque no hay notificación de cambio de propiedad.

**CR-2.3: Sin lógica de auto-naming basada en parámetros**
No existe ningún mecanismo que regenere el nombre del evento cuando cambian sus parámetros característicos (frecuencia, voltajes, duración, etc.).

#### Impacto
- El nombre en timeline/grid siempre es el nombre original del template de la librería
- Confusión al tener múltiples señales con el mismo nombre genérico
- No se pueden distinguir señales visualmente por nombre

---

## 📋 Plan de Implementación

### FASE 1: Corrección de Precisión Temporal
**Estimación:** ~2-3 horas | **Prioridad:** CRÍTICA

| ID | Tarea | Archivo(s) | Complejidad |
|----|-------|-----------|-------------|
| T1.1 | Reemplazar bindings `UpdateSourceTrigger=PropertyChanged` por `UpdateSourceTrigger=LostFocus` + agregar `StringFormat=F6` | `SignalManagerView.xaml` | Baja |
| T1.2 | Crear selector de unidades temporales (ComboBox: s, ms, µs, ns) para StartTime y Duration | `SignalManagerView.xaml`, `SignalManagerViewModel.cs` | Media |
| T1.3 | Implementar propiedades `SelectedEventStartTimeValue`/`SelectedEventStartTimeUnit` con conversión bidireccional | `SignalManagerViewModel.cs` | Media |
| T1.4 | Actualizar labels dinámicos según unidad seleccionada | `SignalManagerView.xaml` | Baja |
| T1.5 | Agregar validación de rangos por unidad (ej: ns no negativo, max coherente con TimeSpan) | `SignalManagerViewModel.cs` | Baja |

#### Diseño Técnico T1.2/T1.3
```
Unidades soportadas:
  s  → factor = 1e9 ns
  ms → factor = 1e6 ns  
  µs → factor = 1e3 ns
  ns → factor = 1 ns

Propiedades nuevas en ViewModel:
  - SelectedStartTimeUnit: string ("s"|"ms"|"µs"|"ns")
  - SelectedDurationUnit: string ("s"|"ms"|"µs"|"ns")
  - SelectedEventStartTimeValue: double (valor en la unidad seleccionada)
  - SelectedEventDurationValue: double (valor en la unidad seleccionada)
  
Conversión:
  SET → TimeSpan.FromTicks((long)(value * factor / 100))
  GET → event.StartTime.Ticks * 100 / factor
```

#### Diseño UI Propuesto
```
Antes:
  [Start Time (s):]  [_________]
  [Duration (ms):]   [_________]

Después:
  [Start Time:]  [_________] [s  ▼]
  [Duration:]    [_________] [ms ▼]
  
  ComboBox opciones: s | ms | µs | ns
```

---

### FASE 2: Corrección de Nomenclatura de Señales
**Estimación:** ~2-3 horas | **Prioridad:** ALTA

| ID | Tarea | Archivo(s) | Complejidad |
|----|-------|-----------|-------------|
| T2.1 | Agregar sincronización de `Name` y `Color` en `OnApplyEventChanges` → DO table | `SignalManagerViewModel.cs` | Baja |
| T2.2 | Implementar `INotifyPropertyChanged` en `SignalEvent` para propiedades clave (Name, StartTime, Duration, Parameters) | `SignalEvent.cs` | Media |
| T2.3 | Implementar lógica de auto-naming: `GenerateAutoName(SignalEvent)` basada en tipo y parámetros | `SignalManagerViewModel.cs` o nueva clase `SignalNameGenerator.cs` | Media |
| T2.4 | Conectar auto-naming al flujo de `OnApplyEventChanges` (con opción de override manual) | `SignalManagerViewModel.cs` | Baja |
| T2.5 | Hacer editable la columna Name del DataGrid (o permitir edición inline) | `SignalManagerView.xaml` | Baja |
| T2.6 | Actualizar `UpdateDisplayText()` en `TimelineEventViewModel` para reflejar cambios de nombre | `TimelineChannelViewModel.cs` | Baja |

#### Diseño de Auto-Naming (T2.3)
```
Reglas por tipo de señal:

DC:       "DC {voltage}V"
          Ejemplo: "DC 5V", "DC 3.3V"

Ramp:     "Ramp {startV}→{endV}V ({duration})"  
          Ejemplo: "Ramp 0→5V (1s)", "Ramp 10→0V (3s)"

Waveform: "Sine {freq} ({amp}V±{offset}V)"
          Ejemplo: "Sine 500Hz (2V±4V)", "Sine 1kHz (3V±5V)"

Digital:  "HIGH", "LOW", "Pulse {duration}"
          Ejemplo: "Pulse 10ms", "HIGH"

Formato de frecuencia inteligente:
  < 1000 Hz  → "{f}Hz"
  >= 1000 Hz → "{f/1000}kHz"
  >= 1e6 Hz  → "{f/1e6}MHz"

Formato de duración inteligente:
  >= 1s     → "{d}s"
  >= 1ms    → "{d*1000}ms"
  >= 1µs    → "{d*1e6}µs"
  < 1µs     → "{d*1e9}ns"
```

#### Propiedad de Control de Auto-Naming
```csharp
// En SignalEvent:
public bool IsNameCustomized { get; set; } = false;

// Lógica:
// - Si IsNameCustomized == false → auto-generar nombre al cambiar parámetros
// - Si el usuario edita Name manualmente → IsNameCustomized = true
// - Botón "Reset Name" → IsNameCustomized = false, regenerar
```

---

## 📊 Indicadores y Métricas de Avance

### Indicadores de Progreso por Fase

| Fase | Indicador | Estado Inicial | Meta | Actual |
|------|-----------|---------------|------|--------|
| F1 | Campos temporales aceptan decimales | ❌ | ✅ | ⬜ Pendiente |
| F1 | Selector de unidades (s/ms/µs/ns) funcional | ❌ | ✅ | ⬜ Pendiente |
| F1 | Conversión bidireccional correcta | ❌ | ✅ | ⬜ Pendiente |
| F1 | Valor "0.75" en campo StartTime(s) se mantiene | ❌ | ✅ | ⬜ Pendiente |
| F1 | Valor "750" en campo Duration(ms) funciona | ❌ | ✅ | ⬜ Pendiente |
| F1 | Valor "750000" en campo Duration(µs) funciona | ❌ | ✅ | ⬜ Pendiente |
| F2 | Name editable en Event Details y se sincroniza al DO table | ❌ | ✅ | ⬜ Pendiente |
| F2 | Name se actualiza en DataGrid al hacer Apply | ❌ | ✅ | ⬜ Pendiente |
| F2 | Name se actualiza en Timeline (bloques visuales) | ❌ | ✅ | ⬜ Pendiente |
| F2 | Auto-naming funciona al cambiar frecuencia | ❌ | ✅ | ⬜ Pendiente |
| F2 | Auto-naming funciona al cambiar voltajes ramp | ❌ | ✅ | ⬜ Pendiente |
| F2 | Override manual de nombre respetado | ❌ | ✅ | ⬜ Pendiente |

### Métricas de Calidad

| Métrica | Criterio de Aceptación |
|---------|----------------------|
| Compilación | 0 errores, 0 warnings nuevos |
| Precisión temporal | Mantiene hasta 100ns de resolución (TimeSpan.Ticks) |
| Consistencia de datos | Nombre en Event Details = DataGrid = Timeline = DO Table |
| Compatibilidad | Secuencias existentes (.json) cargan sin error |
| Usabilidad | Flujo: cambiar parámetro → Apply → nombre actualizado en < 1s |
| Regresión | Tests existentes pasan sin modificación |

### Criterio de Completitud Global

```
FASE 1 COMPLETADA cuando:
  ☐ El usuario puede escribir "0.75" en Start Time (s) sin truncamiento
  ☐ El usuario puede seleccionar "ms" y escribir "750" obteniendo 0.75s
  ☐ El usuario puede seleccionar "µs" y escribir "750000" obteniendo 0.75s
  ☐ Apply Changes persiste los valores correctamente en DO Table
  ☐ La ejecución usa los valores con precisión correcta

FASE 2 COMPLETADA cuando:
  ☐ Editar Name en Event Details + Apply → se ve en DataGrid y Timeline
  ☐ Cambiar frecuencia de 500→1000 + Apply → nombre cambia a "Sine 1kHz..."
  ☐ Cambiar voltajes ramp 0→5 a 0→10 + Apply → nombre cambia a "Ramp 0→10V..."
  ☐ Si el usuario editó nombre manualmente, no se sobreescribe con auto-naming
  ☐ Compilación exitosa con MSBuild
```

---

## 🗂️ Archivos Afectados (Resumen)

| Archivo | Tipo de Cambio | Fase |
|---------|---------------|------|
| `UI/WPF/Views/SignalManager/SignalManagerView.xaml` | Modificar bindings temporales + agregar ComboBox unidades + DataGrid editable | F1, F2 |
| `UI/WPF/ViewModels/SignalManager/SignalManagerViewModel.cs` | Nuevas propiedades de unidad/conversión + fix OnApplyEventChanges + auto-naming | F1, F2 |
| `Core/SignalManager/Models/SignalEvent.cs` | Implementar INotifyPropertyChanged + IsNameCustomized | F2 |
| `UI/WPF/ViewModels/SignalManager/TimelineChannelViewModel.cs` | Actualizar UpdateDisplayText para reflejar cambios | F2 |
| **Nuevo:** `Core/SignalManager/Services/SignalNameGenerator.cs` (opcional) | Lógica de auto-naming centralizada | F2 |

---

## ⚠️ Riesgos y Mitigación

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|-------------|---------|------------|
| Bindings WPF decimal causan loops infinitos | Media | Alto | Usar `UpdateSourceTrigger=LostFocus` + `Delay` |
| INotifyPropertyChanged en SignalEvent rompe serialización JSON | Baja | Medio | JsonIgnore en propiedades de notificación |
| Auto-naming sobreescribe nombres custom del usuario | Media | Alto | Flag `IsNameCustomized` + lógica condicional |
| Conversión ns/µs pierde precisión por float rounding | Baja | Medio | Usar `long` para nanosegundos internos, `double` solo en UI |
| Secuencias guardadas sin IsNameCustomized | Media | Bajo | Default a `false`, backward compatible |

---

## 📝 Notas de Arquitectura

### Flujo actual de datos (timing)
```
UI TextBox → SelectedEventStartSeconds (double, seg) 
           → SelectedEvent.StartTime (TimeSpan)
           → OnApplyEventChanges() 
           → table.StartTimesNs[i] = (long)(StartTime.TotalSeconds * 1e9)
           → Ejecución: nanosegundos
```

### Flujo propuesto de datos (timing)
```
UI TextBox → SelectedEventStartTimeValue (double, en unidad seleccionada)
           → Conversión según SelectedStartTimeUnit
           → SelectedEvent.StartTime (TimeSpan)
           → OnApplyEventChanges()
           → table.StartTimesNs[i] (nanosegundos)
           → Ejecución: nanosegundos
```

### Flujo propuesto de datos (naming)
```
UI TextBox(Name) → SelectedEvent.Name 
                 → IsNameCustomized = true (si editado manualmente)

Cambio de parámetro → OnApplyEventChanges()
                    → if (!IsNameCustomized) GenerateAutoName()
                    → table.Names[i] = newName
                    → UpdateTimeline() → UI refresh
```

---

*Documento generado como plan de investigación. Requiere aprobación antes de implementación.*
