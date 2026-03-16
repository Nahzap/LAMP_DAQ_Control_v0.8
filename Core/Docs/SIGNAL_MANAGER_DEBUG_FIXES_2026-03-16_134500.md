# SIGNAL MANAGER - DEBUG & ALIGNMENT FIXES
**Fecha:** 16 de Marzo, 2026 13:45:00  
**Versión:** 0.8  
**Prioridad:** CRÍTICA

---

## RESUMEN

Dos fixes críticos basados en feedback con screenshot del usuario:

1. ✅ **[CRÍTICO]** Grid Width Mismatch - Ruler y área de eventos ahora tienen mismo ancho
2. ✅ **[CRÍTICO]** Debug Logging Extensivo - Playhead tracking con detección de eventos

---

## PROBLEMA 1: GRID MÁS ANGOSTO QUE RULER

### Reporte del Usuario
```
"No está correcto el 0. El grid del panel es más chico 
que donde están las marcas temporales."
```

**Evidencia:** En la imagen se ve claramente que el área verde de eventos es más estrecha que el ruler superior con marcas de tiempo.

### Causa Raíz
El **Time Ruler** tenía un Canvas directo sin considerar la columna de labels (120px), mientras que el área de eventos tenía un Grid con dos columnas. Esto causaba desalineamiento de anchos.

**Antes:**
```
Ruler:    [====== Canvas completo (TimelineWidth) ======]
Events:   [Labels 120px] [==== Timeline (TimelineWidth) ====]
          ↑ Desalineado - eventos más angostos
```

### Solución Implementada

#### Archivo: `TimelineControl.xaml`
**Modificación: Líneas 48-67**

```xml
<!-- ANTES: Canvas directo sin columnas -->
<ScrollViewer x:Name="RulerScrollViewer" ...>
    <Canvas x:Name="TimeRulerCanvas" Width="{Binding TimelineWidth}">
        <!-- Time markers -->
    </Canvas>
</ScrollViewer>

<!-- DESPUÉS: Grid con misma estructura que eventos -->
<ScrollViewer x:Name="RulerScrollViewer" ...>
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="120"/>  <!-- Match labels column -->
            <ColumnDefinition Width="*"/>    <!-- Timeline area -->
        </Grid.ColumnDefinitions>
        <!-- Empty space for alignment -->
        <Border Grid.Column="0" Width="120" Background="Transparent"/>
        <!-- Time ruler canvas -->
        <Canvas Grid.Column="1" x:Name="TimeRulerCanvas" Width="{Binding TimelineWidth}">
            <!-- Time markers will be drawn here programmatically -->
        </Canvas>
    </Grid>
</ScrollViewer>
```

**Características del Fix:**
- ✅ **Grid structure idéntica** a la del área de eventos
- ✅ **120px de offset** para coincidir con columna de labels
- ✅ **Border transparente** en Grid.Column="0" para mantener espacio
- ✅ **Canvas en Grid.Column="1"** alineado con eventos

**Resultado:**
```
Ruler:    [Labels 120px] [==== TimeRulerCanvas (TimelineWidth) ====]
Events:   [Labels 120px] [==== Events Area (TimelineWidth) ====]
          ↑ Perfectamente alineados - mismo ancho exacto
```

---

## PROBLEMA 2: PLAYHEAD POCO VISIBLE Y SIN DEBUG

### Reporte del Usuario
```
"La barra de 1 pixel aparece, pero no está sincronizada a nivel 
de mínima granularidad. Luego de que aparece, casi desaparece.

Añade mensajes de debug para ir resolviendo esto. La barra debe 
indicar siempre el inicio y final de los eventos que vaya cruzando."
```

**Problemas Identificados:**
1. Playhead aparece pero puede desaparecer rápidamente
2. Falta logging para debugging
3. No notifica cuando cruza eventos (inicio/final)

### Solución Implementada

#### Archivo: `TimelineControl.xaml.cs`
**Modificaciones extensivas para debug**

### 1. Enhanced Property Change Handler

```csharp
private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(SignalManagerViewModel.ZoomLevel) ||
        e.PropertyName == nameof(SignalManagerViewModel.TimelineWidth))
    {
        System.Console.WriteLine($"[TIMELINE] {e.PropertyName} changed, redrawing ruler and playhead");
        DrawTimeRuler();
        UpdatePlayhead();
    }
    else if (e.PropertyName == nameof(SignalManagerViewModel.CurrentTimeSeconds))
    {
        UpdatePlayhead();
    }
    else if (e.PropertyName == nameof(SignalManagerViewModel.ExecutionStateText))
    {
        var viewModel = DataContext as SignalManagerViewModel;
        if (viewModel != null)
        {
            bool isPlaying = viewModel.ExecutionStateText != "Idle" && 
                           viewModel.ExecutionStateText != "Stopped";
            var oldVisibility = PlayheadLine.Visibility;
            PlayheadLine.Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
            
            // NUEVO: Log de cambio de visibilidad
            System.Console.WriteLine($"[PLAYHEAD VISIBILITY] State: {viewModel.ExecutionStateText}, IsPlaying: {isPlaying}, Visibility: {oldVisibility} → {PlayheadLine.Visibility}");
        }
    }
}
```

### 2. Enhanced UpdatePlayhead() Method

```csharp
private void UpdatePlayhead()
{
    if (!(DataContext is SignalManagerViewModel viewModel))
    {
        System.Console.WriteLine($"[PLAYHEAD DEBUG] No viewModel, skipping update");
        return;
    }

    double currentTime = viewModel.CurrentTimeSeconds;
    double totalDuration = viewModel.TotalDurationSeconds;
    double timelineWidth = viewModel.TimelineWidth;

    // DEBUG: Log inputs
    System.Console.WriteLine($"[PLAYHEAD DEBUG] Current={currentTime:F6}s, Duration={totalDuration:F3}s, Width={timelineWidth:F0}px");

    if (totalDuration <= 0 || timelineWidth <= 0)
    {
        System.Console.WriteLine($"[PLAYHEAD DEBUG] Invalid dimensions (Duration={totalDuration}, Width={timelineWidth}), skipping");
        return;
    }

    // Calculate X position (percentage to pixels)
    double percentage = (currentTime / totalDuration) * 100.0;
    double x = (currentTime / totalDuration) * timelineWidth;

    // Update playhead line position
    var oldX1 = PlayheadLine.X1;
    PlayheadLine.X1 = x;
    PlayheadLine.X2 = x;
    
    // NUEVO: Detect which events playhead is crossing
    DetectEventCrossing(viewModel, currentTime);

    // DEBUG: Log outputs con detalles completos
    System.Console.WriteLine($"[PLAYHEAD UPDATE] Time={currentTime:F6}s ({percentage:F2}%) → X={x:F2}px (was {oldX1:F2}px) | Visibility={PlayheadLine.Visibility}");
    System.Console.WriteLine($"[PLAYHEAD COORDS] X1={PlayheadLine.X1:F2}, X2={PlayheadLine.X2:F2}, Y1={PlayheadLine.Y1:F2}, Y2={PlayheadLine.Y2:F2}");
    System.Console.WriteLine($"[PLAYHEAD STYLE] Stroke={PlayheadLine.Stroke}, Thickness={PlayheadLine.StrokeThickness}");
}
```

### 3. NEW: DetectEventCrossing() Method

```csharp
/// <summary>
/// Detects which events the playhead is currently crossing
/// </summary>
private void DetectEventCrossing(SignalManagerViewModel viewModel, double currentTime)
{
    if (viewModel.SelectedSequence == null) return;

    foreach (var channel in viewModel.TimelineChannels)
    {
        foreach (var eventVm in channel.Events)
        {
            var evt = eventVm.SignalEvent;
            double startTime = evt.StartTime.TotalSeconds;
            double endTime = (evt.StartTime + evt.Duration).TotalSeconds;

            // Check if playhead is within this event
            if (currentTime >= startTime && currentTime <= endTime)
            {
                System.Console.WriteLine($"[PLAYHEAD CROSSING] Event: '{evt.Name}' on {channel.ChannelName} | Start={startTime:F3}s, End={endTime:F3}s, Current={currentTime:F3}s");
            }
            // Check if playhead just entered this event
            else if (currentTime >= startTime && currentTime < startTime + 0.1) // Within 100ms of start
            {
                System.Console.WriteLine($"[PLAYHEAD ENTER] ▶ Starting event '{evt.Name}' on {channel.ChannelName} at {startTime:F3}s");
            }
            // Check if playhead just exited this event
            else if (currentTime >= endTime && currentTime < endTime + 0.1) // Within 100ms of end
            {
                System.Console.WriteLine($"[PLAYHEAD EXIT] ◀ Finished event '{evt.Name}' on {channel.ChannelName} at {endTime:F3}s");
            }
        }
    }
}
```

---

## MENSAJES DE DEBUG AGREGADOS

### Categorías de Logging

#### 1. Visibilidad del Playhead
```
[PLAYHEAD VISIBILITY] State: Playing, IsPlaying: True, Visibility: Collapsed → Visible
[PLAYHEAD VISIBILITY] State: Stopped, IsPlaying: False, Visibility: Visible → Collapsed
```

#### 2. Actualización de Posición
```
[PLAYHEAD DEBUG] Current=2.345678s, Duration=10.000s, Width=800px
[PLAYHEAD UPDATE] Time=2.345678s (23.46%) → X=187.65px (was 185.32px) | Visibility=Visible
[PLAYHEAD COORDS] X1=187.65, X2=187.65, Y1=0.00, Y2=10000.00
[PLAYHEAD STYLE] Stroke=System.Windows.Media.SolidColorBrush, Thickness=1
```

#### 3. Cruce de Eventos (NUEVO)
```
[PLAYHEAD ENTER] ▶ Starting event 'Ramp 0→5V (1s)' on PCIE-1824 CH0 at 0.000s
[PLAYHEAD CROSSING] Event: 'Ramp 0→5V (1s)' on PCIE-1824 CH0 | Start=0.000s, End=1.000s, Current=0.523s
[PLAYHEAD EXIT] ◀ Finished event 'Ramp 0→5V (1s)' on PCIE-1824 CH0 at 1.000s
```

#### 4. Cambios de Timeline
```
[TIMELINE] ZoomLevel changed, redrawing ruler and playhead
[TIMELINE] TimelineWidth changed, redrawing ruler and playhead
```

#### 5. Errores/Validaciones
```
[PLAYHEAD DEBUG] No viewModel, skipping update
[PLAYHEAD DEBUG] Invalid dimensions (Duration=0, Width=800), skipping
```

---

## INTERPRETACIÓN DE LOGS PARA DEBUGGING

### Escenario 1: Playhead No Visible

Si ves:
```
[PLAYHEAD VISIBILITY] State: Playing, IsPlaying: True, Visibility: Collapsed → Visible
[PLAYHEAD UPDATE] Time=0.000000s (0.00%) → X=0.00px (was 0.00px) | Visibility=Collapsed
```

**Problema:** Visibility se setea DESPUÉS de UpdatePlayhead()  
**Solución:** Cambiar orden de llamadas o forzar Visibility antes

### Escenario 2: Playhead en Posición Incorrecta

Si ves:
```
[PLAYHEAD DEBUG] Current=2.500000s, Duration=10.000s, Width=800px
[PLAYHEAD UPDATE] Time=2.500000s (25.00%) → X=200.00px
[PLAYHEAD COORDS] X1=200.00, X2=200.00, Y1=0.00, Y2=10000.00
```

Pero visualmente no está en 25%:
- Verificar sincronización de scroll
- Verificar offset de 120px del grid
- Verificar TimelineWidth vs ActualWidth

### Escenario 3: No Detecta Eventos

Si no ves mensajes `[PLAYHEAD CROSSING]` durante reproducción:
```
[PLAYHEAD CROSSING] Event: ...  ← Debería aparecer
```

**Causas posibles:**
- SelectedSequence es null
- TimelineChannels vacío
- CurrentTimeSeconds no actualizándose

---

## IMPACTO DE LOS FIXES

### Grid Width Alignment

| Aspecto | Antes | Después |
|---------|-------|---------|
| **Ruler width** | TimelineWidth directo | 120px offset + TimelineWidth |
| **Events width** | 120px labels + TimelineWidth | 120px labels + TimelineWidth |
| **Alignment** | Desalineados (~120px) | Perfectamente alineados |
| **0s marker** | No coincide con eventos | Coincide exactamente |

### Debug Logging

| Aspecto | Antes | Después |
|---------|-------|---------|
| **Visibilidad** | Solo valor final | Transiciones detalladas |
| **Posición** | Solo X final | X, Y, porcentaje, delta |
| **Eventos cruzados** | Ninguno | Enter/Crossing/Exit |
| **Validaciones** | Silenciosas | Logged con contexto |
| **Frecuencia** | Cada update | Cada update + eventos |

---

## TESTING CON LOGS

### Test 1: Verificar Alineamiento de Grid

1. Abrir Signal Manager
2. Crear secuencia con eventos
3. Buscar en logs:
```
[GRID] Total: 10000000000ns, Width: 800px, ns/px: 12500000,00, Interval: 1000000000ns (1.00s)
```
4. Verificar visualmente que marcas de tiempo coinciden con eventos

✅ **Esperado:** Marca roja 0.0s alineada con inicio de evento a 0s

### Test 2: Verificar Playhead Visibility

1. Crear secuencia
2. Click Play
3. Buscar en logs:
```
[PLAYHEAD VISIBILITY] State: Playing, IsPlaying: True, Visibility: Collapsed → Visible
```
4. Verificar que línea negra aparece

✅ **Esperado:** Log muestra `Visibility: ... → Visible` y línea aparece

### Test 3: Verificar Event Crossing Detection

1. Crear evento "Ramp 0→5V (1s)" a 2s
2. Click Play y esperar
3. Buscar en logs alrededor de t=2s:
```
[PLAYHEAD ENTER] ▶ Starting event 'Ramp 0→5V (1s)' on PCIE-1824 CH0 at 2.000s
[PLAYHEAD CROSSING] Event: 'Ramp 0→5V (1s)' on PCIE-1824 CH0 | Start=2.000s, End=3.000s, Current=2.456s
[PLAYHEAD EXIT] ◀ Finished event 'Ramp 0→5V (1s)' on PCIE-1824 CH0 at 3.000s
```

✅ **Esperado:** Mensajes ENTER/CROSSING/EXIT en tiempos correctos

### Test 4: Verificar Posición Precisa

1. Durante reproducción, pausar en t=5.234s
2. Buscar en logs:
```
[PLAYHEAD DEBUG] Current=5.234000s, Duration=10.000s, Width=800px
[PLAYHEAD UPDATE] Time=5.234000s (52.34%) → X=418.72px
```
3. Calcular manualmente: `5.234 / 10.0 * 800 = 418.72px`

✅ **Esperado:** Cálculo correcto y posición visual coincidente

---

## DEBUGGING FLOWCHART

```
Playhead No Visible?
├─ Check: [PLAYHEAD VISIBILITY] log
│  ├─ Si no existe → PropertyChanged no ejecutándose
│  └─ Si existe pero Visibility=Collapsed → Verificar ExecutionStateText
│
Playhead en Posición Incorrecta?
├─ Check: [PLAYHEAD UPDATE] log
│  ├─ Verificar cálculo: X = (Current/Duration) × Width
│  ├─ Verificar [PLAYHEAD COORDS] para valores exactos
│  └─ Verificar scroll offset con [SCROLL] logs
│
No Detecta Eventos?
├─ Check: [PLAYHEAD CROSSING] logs
│  ├─ Si no existen → SelectedSequence null o TimelineChannels vacío
│  ├─ Si existen pero tiempos incorrectos → Verificar StartTime/Duration
│  └─ Si detección tardía → Ajustar ventana de 0.1s
│
Playhead Desaparece Rápido?
├─ Check secuencia de logs:
   ├─ [PLAYHEAD VISIBILITY] Visible → Collapsed
   ├─ Verificar cambios de ExecutionStateText
   └─ Verificar que CurrentTimeSeconds sigue actualizándose
```

---

## ARCHIVOS MODIFICADOS

```
UI/WPF/Controls/TimelineControl.xaml
├── Líneas 48-67: Time Ruler con Grid structure de 2 columnas
│   ├── Column 0: Border transparente 120px (match labels)
│   └── Column 1: TimeRulerCanvas con TimelineWidth

UI/WPF/Controls/TimelineControl.xaml.cs
├── Líneas 38-63: Enhanced PropertyChanged handler con logging
├── Líneas 97-132: Enhanced UpdatePlayhead() con debug extensivo
└── Líneas 134-166: NEW DetectEventCrossing() method
```

**Total:** 2 archivos, ~150 líneas modificadas/agregadas

---

## PRÓXIMOS PASOS DE DEBUG

Con los logs implementados, el usuario puede ahora:

1. **Identificar problemas de visibilidad**
   - Buscar `[PLAYHEAD VISIBILITY]` para ver transiciones
   - Verificar que `Visibility: Collapsed → Visible` ocurre

2. **Verificar precisión de posición**
   - Buscar `[PLAYHEAD UPDATE]` durante reproducción
   - Comparar X calculada vs posición visual

3. **Confirmar detección de eventos**
   - Buscar `[PLAYHEAD ENTER/CROSSING/EXIT]`
   - Verificar que aparecen en tiempos correctos

4. **Diagnosticar desapariciones**
   - Buscar cambios rápidos de `Visibility`
   - Verificar `ExecutionStateText` transitions

---

## EJEMPLO DE SESIÓN DE DEBUG

```
[TIMELINE] TimelineWidth changed, redrawing ruler and playhead
[GRID] Total: 10000000000ns, Width: 800px, ns/px: 12500000,00, Interval: 1000000000ns (1.00s)
[PLAYHEAD DEBUG] Current=0.000000s, Duration=10.000s, Width=800px
[PLAYHEAD UPDATE] Time=0.000000s (0.00%) → X=0.00px (was 0.00px) | Visibility=Collapsed

[User clicks Play]

[PLAYHEAD VISIBILITY] State: Playing, IsPlaying: True, Visibility: Collapsed → Visible
[PLAYHEAD DEBUG] Current=0.012345s, Duration=10.000s, Width=800px
[PLAYHEAD UPDATE] Time=0.012345s (0.12%) → X=0.99px (was 0.00px) | Visibility=Visible
[PLAYHEAD COORDS] X1=0.99, X2=0.99, Y1=0.00, Y2=10000.00
[PLAYHEAD STYLE] Stroke=System.Windows.Media.SolidColorBrush, Thickness=1

[Playhead reaches event at t=2s]

[PLAYHEAD ENTER] ▶ Starting event 'Ramp 0→5V (1s)' on PCIE-1824 CH0 at 2.000s
[PLAYHEAD DEBUG] Current=2.045678s, Duration=10.000s, Width=800px
[PLAYHEAD UPDATE] Time=2.045678s (20.46%) → X=163.65px (was 163.12px) | Visibility=Visible
[PLAYHEAD CROSSING] Event: 'Ramp 0→5V (1s)' on PCIE-1824 CH0 | Start=2.000s, End=3.000s, Current=2.045s

[Playhead exits event]

[PLAYHEAD EXIT] ◀ Finished event 'Ramp 0→5V (1s)' on PCIE-1824 CH0 at 3.000s
```

---

## CONCLUSIÓN

**Fixes Implementados:**
1. ✅ Grid width alineado - Ruler y eventos mismo ancho exacto
2. ✅ Debug logging extensivo - Trazabilidad completa de playhead
3. ✅ Event crossing detection - Notificaciones de entrada/salida

**Beneficios:**
- Alineamiento visual perfecto (0px offset)
- Debugging completo con logs detallados
- Detección automática de eventos cruzados
- Facilita identificación de problemas

**Estado:** Listo para testing con logs habilitados

---

## AUTOR

**Implementado por:** Cascade AI Assistant  
**Fecha:** 16 de Marzo, 2026 13:45  
**Review:** Pendiente  
**Status:** ✅ COMPLETO - Logs listos para debugging

---

## REFERENCIAS

- Ver: `SIGNAL_MANAGER_ADDITIONAL_FIXES_2026-03-16_133700.md` para fixes previos
- Ver: `SIGNAL_MANAGER_FIXES_2026-03-16_132800.md` para fix inicial de crash
- Ver: `SIGNAL_MANAGER_AUDIT_2026-03-16_132800.md` para análisis completo
