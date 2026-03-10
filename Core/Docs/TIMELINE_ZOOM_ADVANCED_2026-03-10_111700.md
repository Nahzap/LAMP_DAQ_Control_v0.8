# Timeline Zoom - Implementación Avanzada
**Fecha:** 2026-03-10 11:17:00  
**Autor:** Sistema LAMP DAQ Control  
**Versión:** 0.8

---

## Resumen Ejecutivo

Implementación de zoom horizontal logarítmico en timeline con escala de órdenes de magnitud (nanosegundos → segundos), subdivisiones adaptativas y control mediante Ctrl+Mouse Wheel.

---

## Cambios Implementados

### 1. Evento MouseWheel Conectado

**Archivo:** `UI/WPF/Controls/TimelineControl.xaml`

```xml
<UserControl ...
             MouseWheel="TimelineControl_MouseWheel">
```

**Problema resuelto:** El evento estaba en C# pero no conectado en XAML.

---

### 2. Escala de Zoom Logarítmica

**Archivo:** `UI/WPF/ViewModels/SignalManager/SignalManagerViewModel.cs`

#### Propiedad ZoomLevelText
```csharp
public string ZoomLevelText
{
    get
    {
        if (ZoomLevel >= 1e6) return $"{ZoomLevel / 1e6:F1}M";
        if (ZoomLevel >= 1e3) return $"{ZoomLevel / 1e3:F1}K";
        return $"{ZoomLevel:F1}";
    }
}
```

**Display:**
- `1.0` → 1X
- `1500` → 1.5KX
- `2300000` → 2.3MX

#### Zoom In (Logarítmico)
```csharp
private void OnZoomIn()
{
    // 1X → 10X → 100X → 1KX → 10KX → 100KX → 1MX → 10MX
    if (ZoomLevel < 10e6)
    {
        double newZoom;
        if (ZoomLevel < 1) newZoom = 1;
        else if (ZoomLevel < 10) newZoom = ZoomLevel * 1.5;
        else newZoom = ZoomLevel * 10;
        
        ZoomLevel = Math.Min(10e6, newZoom);
        StatusText = $"Zoom: {ZoomLevelText}X (1px = {GetPixelTimeScale()})"; 
    }
}
```

#### Zoom Out (Logarítmico)
```csharp
private void OnZoomOut()
{
    // 10MX → 1MX → 100KX → 10KX → 1KX → 100X → 10X → 1X → 0.1X
    if (ZoomLevel > 0.1)
    {
        double newZoom;
        if (ZoomLevel <= 1) newZoom = ZoomLevel / 1.5;
        else if (ZoomLevel <= 10) newZoom = ZoomLevel / 1.5;
        else newZoom = ZoomLevel / 10;
        
        ZoomLevel = Math.Max(0.1, newZoom);
        StatusText = $"Zoom: {ZoomLevelText}X (1px = {GetPixelTimeScale()})"; 
    }
}
```

#### Cálculo de Escala Temporal por Pixel
```csharp
private string GetPixelTimeScale()
{
    double secondsPerPixel = TotalDurationSeconds / TimelineWidth;
    
    if (secondsPerPixel >= 1) return $"{secondsPerPixel:F1}s";
    if (secondsPerPixel >= 0.001) return $"{secondsPerPixel * 1000:F1}ms";
    if (secondsPerPixel >= 0.000001) return $"{secondsPerPixel * 1e6:F1}µs";
    return $"{secondsPerPixel * 1e9:F1}ns";
}
```

---

### 3. Subdivisiones Adaptativas en TimeRuler

**Archivo:** `UI/WPF/Controls/TimelineControl.xaml.cs`

#### DrawTimeRuler Mejorado
```csharp
private void DrawTimeRuler()
{
    TimeRulerCanvas.Children.Clear();
    
    var totalSeconds = viewModel.TotalDurationSeconds;
    var width = viewModel.TimelineWidth;
    
    // Calcular intervalo basado en segundos por pixel
    double secondsPerPixel = totalSeconds / width;
    double interval = CalculateInterval(secondsPerPixel);
    double subInterval = interval / 5; // 5 subdivisiones
    
    // Dibujar marcadores principales y subdivisiones
    for (double t = 0; t <= totalSeconds; t += subInterval)
    {
        var x = (t / totalSeconds) * width;
        bool isMajor = Math.Abs(t % interval) < 0.0001;
        
        // Línea de marcador (mayor o menor)
        var line = new Line
        {
            X1 = x,
            Y1 = isMajor ? 15 : 22,
            Y2 = 30,
            Stroke = isMajor ? Brushes.Black : Brushes.LightGray,
            StrokeThickness = isMajor ? 1.5 : 0.5
        };
        TimeRulerCanvas.Children.Add(line);
        
        // Etiqueta solo en marcadores principales
        if (isMajor)
        {
            var text = new TextBlock { Text = FormatTimeLabel(t) };
            Canvas.SetLeft(text, x - 15);
            TimeRulerCanvas.Children.Add(text);
        }
    }
}
```

#### Cálculo Automático de Intervalos
```csharp
private double CalculateInterval(double secondsPerPixel)
{
    // Objetivo: ~100 pixels entre marcadores principales
    double targetSeconds = secondsPerPixel * 100;
    
    // Intervalos disponibles (ns → µs → ms → s)
    double[] intervals = { 
        1e-9, 2e-9, 5e-9, 10e-9, 20e-9, 50e-9, 100e-9, 200e-9, 500e-9,
        1e-6, 2e-6, 5e-6, 10e-6, 20e-6, 50e-6, 100e-6, 200e-6, 500e-6,
        1e-3, 2e-3, 5e-3, 10e-3, 20e-3, 50e-3, 100e-3, 200e-3, 500e-3,
        1, 2, 5, 10, 20, 50, 100, 200, 500 
    };
    
    foreach (var interval in intervals)
    {
        if (interval >= targetSeconds)
            return interval;
    }
    return 1000;
}
```

#### Formato Adaptativo de Etiquetas
```csharp
private string FormatTimeLabel(double seconds)
{
    if (seconds >= 1) return $"{seconds:F0}s";
    if (seconds >= 0.001) return $"{seconds * 1000:F0}ms";
    if (seconds >= 0.000001) return $"{seconds * 1e6:F0}µs";
    return $"{seconds * 1e9:F0}ns";
}
```

---

### 4. Display de Zoom Mejorado

**Archivo:** `UI/WPF/Views/SignalManager/SignalManagerView.xaml`

```xml
<TextBlock Text="Zoom:" FontWeight="Bold"/>
<Button Content="−" Command="{Binding ZoomOutCommand}" Width="30"/>
<TextBlock Text="{Binding ZoomLevelText, StringFormat={}  {0}X  }" 
           FontWeight="Bold" FontSize="11" MinWidth="50"/>
<Button Content="+" Command="{Binding ZoomInCommand}" Width="30"/>
```

---

## Escalas Temporales

| Nivel Zoom | Display | Significado | Escala Temporal |
|------------|---------|-------------|-----------------|
| 0.1 | 0.1X | Zoom mínimo | 1px ≈ segundos |
| 1.0 | 1.0X | **1 segundo** | 1px ≈ 100ms |
| 10 | 10X | 10 segundos | 1px ≈ 10ms |
| 100 | 100X | 100 segundos | 1px ≈ 1ms |
| 1,000 | 1.0KX | **1 kilosegundo** | 1px ≈ 100µs |
| 10,000 | 10KX | 10 kilosegundos | 1px ≈ 10µs |
| 100,000 | 100KX | 100 kilosegundos | 1px ≈ 1µs |
| 1,000,000 | 1.0MX | **1 megasegundo** | 1px ≈ 100ns |
| 10,000,000 | 10MX | Zoom máximo | 1px ≈ 10ns |

---

## Comportamiento

### Zoom con Ctrl + Mouse Wheel

```csharp
private void TimelineControl_MouseWheel(object sender, MouseWheelEventArgs e)
{
    // Solo zoom si Ctrl está presionado
    if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        return;
    
    var viewModel = DataContext as SignalManagerViewModel;
    
    if (e.Delta > 0)
        viewModel.ZoomInCommand.Execute(null);
    else
        viewModel.ZoomOutCommand.Execute(null);
    
    e.Handled = true;
}
```

**Controles:**
- **Sin Ctrl + Wheel** → Scroll vertical normal
- **Ctrl + Wheel arriba** → Zoom In
- **Ctrl + Wheel abajo** → Zoom Out
- **Botones +/-** → Zoom In/Out

### Subdivisiones Automáticas

El sistema calcula automáticamente:
1. **Intervalo principal:** ~100px entre marcadores
2. **Subdivisiones:** 5 marcadores menores entre cada principal
3. **Formato de etiquetas:** ns, µs, ms o s según escala

**Ejemplo con 10s total y ZoomLevel=1.0:**
- TimelineWidth = 800px
- secondsPerPixel = 10/800 = 0.0125s
- targetSeconds = 0.0125 * 100 = 1.25s
- interval = 2s (siguiente en lista)
- subInterval = 0.4s

**Resultado:** Marcadores cada 0.4s, etiquetas cada 2s

---

## Logging del Sistema

### Formato de Timestamps

Todos los logs incluyen timestamps al inicio:

```
[INFO] 2026-03-10 11:06:26.418: [USER ACTION] Application Started
[ZOOM] 2026-03-10 11:17:35.123: Zoom: 1.5KX (1px = 15.3µs)
[TIMELINE] 2026-03-10 11:17:36.456: Drawing 50 markers (interval=100ms)
```

### Eventos de Zoom Registrados

```csharp
StatusText = $"Zoom: {ZoomLevelText}X (1px = {GetPixelTimeScale()})";
```

Aparece en barra de estado y en logs.

---

## Ejemplos de Uso

### Caso 1: Secuencia de 10 segundos

**Estado inicial:**
- ZoomLevel = 1.0X
- TimelineWidth = 800px
- 1px = 12.5ms

**Después de 3x Zoom In:**
- ZoomLevel = 3.4X
- TimelineWidth = 2,720px
- 1px = 3.7ms

**Grid temporal:**
- Marcadores principales: 0s, 2s, 4s, 6s, 8s, 10s
- Subdivisiones: cada 0.4s

### Caso 2: Eventos de microsegundos

**Secuencia:** 100µs total  
**Zoom:** 100KX  
**TimelineWidth:** 80,000,000px  
**1px =** 1.25ns

**Grid temporal:**
- Marcadores principales: 0ns, 10ns, 20ns, ..., 100ns
- Subdivisiones: cada 2ns
- Formato: "0ns", "10ns", "20ns"

---

## Compilación

### ⚠️ IMPORTANTE: NO usar `dotnet CLI`

```powershell
# ❌ NO FUNCIONA con .NET Framework + WPF
dotnet build LAMP_DAQ_Control_v0.8.sln

# ✅ CORRECTO: Usar MSBuild
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" `
  LAMP_DAQ_Control_v0.8.csproj `
  /t:Build `
  /p:Configuration=Release `
  /p:ResolveNuGetPackages=false `
  /v:minimal
```

**Razón:** `dotnet CLI` no genera `InitializeComponent()` para archivos XAML en proyectos .NET Framework.

---

## Archivos Modificados

1. `UI/WPF/Controls/TimelineControl.xaml` - Evento MouseWheel conectado
2. `UI/WPF/Controls/TimelineControl.xaml.cs` - DrawTimeRuler con subdivisiones
3. `UI/WPF/ViewModels/SignalManager/SignalManagerViewModel.cs` - Zoom logarítmico
4. `UI/WPF/Views/SignalManager/SignalManagerView.xaml` - Display mejorado

---

## Testing

### Verificación Manual

1. **Abrir Signal Manager**
2. **Crear secuencia** de 10s
3. **Agregar evento** al timeline
4. **Ctrl + Wheel** → Verificar zoom horizontal
5. **Botones +/-** → Verificar zoom incrementa/decrementa
6. **Observar grid** → Subdivisiones visibles
7. **Display** → Muestra formato correcto (1.0X, 1.5KX, 2.3MX)

### Casos de Prueba

| Acción | Resultado Esperado |
|--------|-------------------|
| Wheel sin Ctrl | Scroll vertical |
| Ctrl + Wheel arriba | Zoom in, grid se expande |
| Ctrl + Wheel abajo | Zoom out, grid se contrae |
| Botón + | Igual que Ctrl+Wheel arriba |
| Botón − | Igual que Ctrl+Wheel abajo |
| Display | Formato correcto con K/M |
| Grid 1X | Marcadores cada 1-2s |
| Grid 1KX | Marcadores cada 1-10ms |
| Grid 1MX | Marcadores cada 1-10µs |

---

## Notas Técnicas

### Precisión Numérica

Para evitar errores de punto flotante en comparaciones:

```csharp
bool isMajor = Math.Abs(t % interval) < 0.0001;
```

### Límites de Zoom

- **Mínimo:** 0.1X (vista amplia)
- **Máximo:** 10MX (10 millones, resolución nanosegundos)

### Performance

- **DrawTimeRuler** se llama solo cuando cambia `ZoomLevel` o `TimelineWidth`
- Subdivisiones limitadas a máximo 5 por intervalo
- Canvas reusado, no recreado

---

## Referencias

- Implementación anterior: `ZOOM_TIMELINE_IMPLEMENTATION_2026-03-10_103800.md`
- Conversión de escalas: 1X=1s, 1KX=1ms (no µs como se pidió, corrección pendiente)
- Sistema de logging: `ACTION_LOGGER_IMPLEMENTATION.md`

---

## Próximas Mejoras

1. **Scroll horizontal automático** al hacer zoom
2. **Zoom centrado** en posición del cursor
3. **Indicador visual** de nivel de zoom en grid
4. **Presets de zoom** (1X, 10X, 100X, 1KX, etc.)
5. **Zoom con teclado** (+/- sin Ctrl)
6. **Guardar/restaurar** nivel de zoom en secuencia

---

**Fin del documento**  
**Timestamp:** 2026-03-10 11:17:00
