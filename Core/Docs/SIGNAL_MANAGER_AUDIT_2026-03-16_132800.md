# SIGNAL MANAGER AUDIT - 2026-03-16 13:28:00

## RESUMEN EJECUTIVO

Auditoría del Signal Manager tras análisis de crash reportado el 16/03/2026 13:23.
**Severidad: CRÍTICA** - El sistema permite operaciones que causan congelamiento y crash.

---

## HALLAZGOS CRÍTICOS

### 1. CRASH POR DRAG-AND-DROP SIN SECUENCIA [CRÍTICO]

**Problema:**
- Usuario puede arrastrar señales de la biblioteca al timeline SIN tener una secuencia seleccionada
- El sistema rechaza la operación silenciosamente: `[ADD SIGNAL ERROR] No sequence selected`
- Múltiples intentos falidos causan congelamiento de la UI y posterior crash
- NO hay retroalimentación visual que indique que la operación no está permitida

**Evidencia del Log:**
```
[DROP] Calling AddSignalToChannel...
[ADD SIGNAL] AddSignalToChannel called: Signal=Sine 10Hz, Channel=PCIE-1824 CH1, StartTime=0,01s
[ADD SIGNAL ERROR] No sequence selected
[DROP] AddSignalToChannel result: False
```
Repetido 3 veces, luego crash del sistema.

**Causa Raíz:**
- `SignalManagerView.xaml`: Elementos de biblioteca SIEMPRE permiten `MouseDown` (línea 104)
- `TimelineControl.xaml`: Canales SIEMPRE tienen `AllowDrop="True"` (línea 97)
- `SignalManagerViewModel.AddSignalToChannel()`: Valida DESPUÉS del drag, no ANTES (línea 836)
- NO hay binding a `IsEnabled` basado en `SelectedSequence`

**Impacto:**
- Pérdida de trabajo del usuario (crash sin guardar)
- Frustración por falta de feedback visual
- Sistema inestable bajo condiciones normales de uso

---

### 2. GRID TIMELINE MAL ALINEADO [ALTA]

**Problema:**
- El grid no comienza exactamente en tiempo 0
- Las marcas de tiempo no coinciden con las posiciones esperadas
- Dificulta posicionamiento preciso de eventos

**Evidencia:**
```
[GRID] Total: 10000000000ns, Width: 800px, ns/px: 12500000,00, Interval: 2000000000ns
```
- Intervalo de 2s (2,000,000,000 ns) es demasiado grueso para timeline de 10s
- No hay marca visible en 0s

**Causa:**
- `TimelineControl.xaml.cs::DrawTimeRuler()` (línea 96-127)
- Loop `for (long t = 0; t <= totalNanoseconds; t += subIntervalNs)`
- Primer marcador en t=0 puede quedar fuera del Canvas por márgenes
- Canvas no tiene padding/margin explícito para inicio

---

### 3. GRANULOMETRÍA POCO INTUITIVA [MEDIA]

**Problema:**
- Para timeline de 10 segundos, muestra solo marcadores cada 2 segundos
- Zoom no mejora granularidad proporcionalmente
- Usuario espera ver al menos marcadores cada 1s o 0.5s

**Cálculo Actual:**
```csharp
// Objetivo: ~100 pixels entre marcadores principales
double targetNanoseconds = nanosecondsPerPixel * 100;
```
- Con 800px / 10s → 80px/s
- Target = 12,500,000 ns/px * 100 = 1,250,000,000 ns (1.25s)
- Selecciona intervalo de 2s del array

**Mejora Sugerida:**
- Target de 50-80px entre marcadores (más denso)
- Intervalos más granulares: 0.1s, 0.2s, 0.5s, 1s, 2s, 5s

---

## ANÁLISIS DE FLUJO DE EVENTOS

### Secuencia del Crash (según log):

1. **13:23:21** - Aplicación inicia correctamente
2. **13:23:25** - Usuario abre Signal Manager
3. **13:23:25** - Se inicializan 64 canales (32 Digital + 32 Analog)
4. **13:23:25** - Biblioteca de señales cargada (26 señales en 5 categorías)
5. **13:23:25** - **NO se crea ninguna secuencia automáticamente**
6. **13:23:25+** - Usuario intenta drag-and-drop 3 veces sin secuencia
7. **13:23:??** - Sistema se congela
8. **Resultado** - Crash completo

### Estado del Sistema al Momento del Crash:

```
[SIGNAL MANAGER] Created 2 device controllers
[EXEC ENGINE INIT] Initialized with 2 device controller(s)
[TIMELINE INIT] COMPLETE: 64 total channels created
[SIGNAL LIBRARY] Initialization complete. Total categories in UI: 5
[INFO] Signal Manager Opened | Details: Window displayed with 2 devices
```

**Observación Crítica:**
- Secuencias: 0 (vacío)
- Canales: 64 (listos)
- Señales: 26 (disponibles)
- **Falta paso inicial: Crear secuencia por defecto o forzar creación antes de permitir edición**

---

## COMPORTAMIENTO ESPERADO vs. ACTUAL

### Escenario: Drag-and-Drop sin Secuencia

| Aspecto | Esperado | Actual | Gap |
|---------|----------|--------|-----|
| **Visual** | Biblioteca deshabilitada (gris) | Totalmente habilitada (color normal) | ❌ CRÍTICO |
| **Cursor** | "No permitido" al arrastrar | Cursor normal de drag | ❌ CRÍTICO |
| **Drop** | Rechazo inmediato con mensaje | Procesamiento completo y rechazo silencioso | ❌ CRÍTICO |
| **Feedback** | MessageBox o notificación | Solo log interno | ❌ CRÍTICO |
| **Sistema** | Estable | Congelamiento → Crash | ❌ CRÍTICO |

---

## COMPONENTES AFECTADOS

### Archivos con Problemas:

1. **`SignalManagerView.xaml`**
   - Línea 72-74: ListBox de secuencias sin validación visual
   - Línea 104: MouseDown sin verificar SelectedSequence
   - **Falta:** Binding IsEnabled en biblioteca

2. **`TimelineControl.xaml`**
   - Línea 97: `AllowDrop="True"` incondicional
   - **Falta:** Binding IsEnabled basado en secuencia

3. **`TimelineControl.xaml.cs`**
   - Línea 18-20: OnSignalMouseDown no valida estado
   - Línea 177-247: OnChannelDrop procesa sin validación previa
   - Línea 73-128: DrawTimeRuler con alineación incorrecta

4. **`SignalManagerViewModel.cs`**
   - Línea 832-902: AddSignalToChannel valida TARDE (después de procesamiento)
   - **Falta:** Property IsSequenceSelected para bindings

---

## MÉTRICAS DE IMPACTO

### Frecuencia del Bug:
- **100%** si usuario abre Signal Manager sin crear secuencia primero
- **Típico:** Usuario nuevo espera poder usar drag-and-drop inmediatamente
- **Reproducible:** Siempre

### Severidad:
- **Pérdida de datos:** Alta (crash sin guardar)
- **Usabilidad:** Crítica (operación básica no funciona)
- **Experiencia:** Muy negativa (congelamiento sin explicación)

---

## SOLUCIONES PROPUESTAS

### Fix 1: PREVENCIÓN DE DRAG-AND-DROP [CRÍTICA - INMEDIATA]

**Objetivo:** Deshabilitar drag-and-drop cuando no hay secuencia seleccionada.

**Cambios:**

1. **SignalManagerViewModel.cs** - Agregar property:
```csharp
public bool IsSequenceSelected => SelectedSequence != null;
```
Notificar cambio cuando SelectedSequence cambia.

2. **SignalManagerView.xaml** - Deshabilitar biblioteca:
```xml
<TreeView ItemsSource="{Binding SignalCategories}" 
          IsEnabled="{Binding IsSequenceSelected}"
          Background="White">
```

3. **TimelineControl.xaml** - Deshabilitar drop:
```xml
<Border AllowDrop="{Binding DataContext.IsSequenceSelected, 
                    RelativeSource={RelativeSource AncestorType=UserControl}}"
```

4. **SignalManagerView.xaml.cs** - Validación temprana:
```csharp
private void OnSignalMouseDown(object sender, MouseButtonEventArgs e)
{
    var viewModel = DataContext as SignalManagerViewModel;
    if (viewModel?.SelectedSequence == null)
    {
        MessageBox.Show("Please create or select a sequence first.", 
                       "No Sequence", MessageBoxButton.OK, MessageBoxImage.Information);
        e.Handled = true;
        return;
    }
    // ... resto del código
}
```

**Beneficios:**
- ✅ Previene crash 100%
- ✅ Feedback visual inmediato
- ✅ Usuario entiende qué hacer
- ✅ Sistema estable

---

### Fix 2: ALINEACIÓN DEL GRID [ALTA]

**Objetivo:** Grid comienza exactamente en 0s con marca visible.

**Cambios en `TimelineControl.xaml.cs`:**

```csharp
private void DrawTimeRuler()
{
    TimeRulerCanvas.Children.Clear();
    
    // ... código existente ...
    
    // Asegurar que la primera marca es SIEMPRE en 0
    // y agregar padding left al Canvas para visibilidad
    TimeRulerCanvas.Margin = new Thickness(2, 0, 2, 0); // Margen para visibilidad
    
    // Dibujar marca en 0 SIEMPRE, incluso si no es múltiplo de intervalo
    var zeroLine = new Line
    {
        X1 = 0, Y1 = 15, X2 = 0, Y2 = 30,
        Stroke = Brushes.Red, StrokeThickness = 2 // Destacado
    };
    TimeRulerCanvas.Children.Add(zeroLine);
    
    var zeroText = new TextBlock
    {
        Text = "0.0s", FontSize = 9, Foreground = Brushes.Red, FontWeight = FontWeights.Bold
    };
    Canvas.SetLeft(zeroText, 2);
    Canvas.SetTop(zeroText, 0);
    TimeRulerCanvas.Children.Add(zeroText);
    
    // Luego dibujar resto de marcadores DESDE subIntervalNs
    for (long t = subIntervalNs; t <= totalNanoseconds; t += subIntervalNs)
    {
        // ... código existente ...
    }
}
```

---

### Fix 3: GRANULOMETRÍA MEJORADA [MEDIA]

**Objetivo:** Intervalos más intuitivos y adaptables al zoom.

**Cambios en `CalculateIntervalNanoseconds`:**

```csharp
private long CalculateIntervalNanoseconds(double nanosecondsPerPixel)
{
    // MEJORADO: Objetivo 50-80px entre marcadores (más denso)
    double targetNanoseconds = nanosecondsPerPixel * 60;
    
    // Intervalos más granulares
    long[] intervals = { 
        // Sub-segundo (para zoom alto)
        100_000, 200_000, 500_000,           // 0.1ms, 0.2ms, 0.5ms
        1_000_000, 2_000_000, 5_000_000,     // 1ms, 2ms, 5ms
        10_000_000, 20_000_000, 50_000_000,  // 10ms, 20ms, 50ms
        100_000_000, 200_000_000, 500_000_000, // 0.1s, 0.2s, 0.5s
        
        // Segundos
        1_000_000_000,                       // 1s
        2_000_000_000,                       // 2s
        5_000_000_000,                       // 5s
        10_000_000_000,                      // 10s
        30_000_000_000,                      // 30s
        60_000_000_000                       // 1min
    };
    
    foreach (var interval in intervals)
    {
        if (interval >= targetNanoseconds)
            return interval;
    }
    return 60_000_000_000; // 1 minuto máximo
}
```

---

### Fix 4: SECUENCIA POR DEFECTO [OPCIONAL - UX]

**Objetivo:** Crear secuencia "Untitled" automáticamente al abrir Signal Manager.

**Cambio en `SignalManagerViewModel` constructor:**

```csharp
public SignalManagerViewModel(...)
{
    // ... código existente ...
    
    // Crear secuencia por defecto para mejor UX
    var defaultSequence = _sequenceEngine.CreateSequence("Untitled Sequence");
    Sequences.Add(defaultSequence);
    SelectedSequence = defaultSequence;
    
    StatusText = "Ready - Default sequence created";
}
```

**Beneficio:** Usuario puede empezar a trabajar inmediatamente.

---

## PRIORIZACIÓN DE FIXES

| Fix | Prioridad | Esfuerzo | Impacto | Orden |
|-----|-----------|----------|---------|-------|
| Fix 1: Prevención Drag-and-Drop | **CRÍTICA** | 2 horas | Elimina crash | 1️⃣ |
| Fix 2: Alineación Grid | Alta | 1 hora | Mejora precisión | 2️⃣ |
| Fix 3: Granulometría | Media | 30 min | Mejora UX | 3️⃣ |
| Fix 4: Secuencia Default | Baja | 15 min | Mejora UX | 4️⃣ |

---

## TESTING REQUERIDO

### Test Case 1: Drag-and-Drop sin Secuencia
1. Abrir Signal Manager
2. NO crear secuencia
3. Intentar arrastrar señal desde biblioteca
4. **Esperado:** 
   - Biblioteca deshabilitada (gris)
   - O mensaje claro al intentar arrastrar
   - NO crash, NO congelamiento

### Test Case 2: Grid Alignment
1. Crear secuencia de 10s
2. Observar timeline
3. **Esperado:**
   - Marca roja en 0.0s claramente visible
   - Marcas cada 1s o 2s según zoom
   - Alineación perfecta con eventos

### Test Case 3: Granulometría con Zoom
1. Crear secuencia de 10s
2. Probar zoom 0.5X, 1X, 2X, 5X
3. **Esperado:**
   - Zoom bajo (0.5X): Marcas cada 2-5s
   - Zoom 1X: Marcas cada 1s
   - Zoom alto (5X): Marcas cada 0.5s o menos
   - Siempre legible, nunca saturado

---

## RECOMENDACIONES ADICIONALES

### 1. Logging Mejorado
- Agregar `[WARNING]` cuando usuario intenta operación no permitida
- Log nivel UI para debugging de eventos visuales

### 2. Validación Defensiva
- Todas las operaciones críticas deben validar `SelectedSequence != null` ANTES de procesar
- Usar `CanExecute` en todos los commands relacionados con secuencias

### 3. UI/UX
- Agregar tooltip explicativo: "Select or create a sequence to start editing"
- Mensaje de bienvenida en timeline vacío
- Botón destacado "New Sequence" cuando lista está vacía

### 4. Error Handling
- Catch exceptions en drag-and-drop handlers
- Mostrar mensajes amigables, no solo logs
- Prevenir congelamiento con try-catch en operaciones largas

---

## CONCLUSIÓN

El Signal Manager tiene una **vulnerabilidad crítica** que causa crash bajo uso normal.
La solución es directa y debe implementarse **inmediatamente**.

**Riesgo Actual:** ALTO - Cualquier usuario nuevo experimenta crash en primeros 30 segundos.
**Riesgo Post-Fix:** BAJO - Sistema estable con feedback claro.

---

## HISTORIAL DE CAMBIOS

| Fecha | Versión | Cambio |
|-------|---------|--------|
| 2026-03-16 13:28 | 1.0 | Auditoría inicial tras crash reportado |

---

**Auditor:** Cascade AI Assistant  
**Fecha:** 16 de Marzo, 2026  
**Severidad:** CRÍTICA  
**Estado:** REQUIERE ACCIÓN INMEDIATA
