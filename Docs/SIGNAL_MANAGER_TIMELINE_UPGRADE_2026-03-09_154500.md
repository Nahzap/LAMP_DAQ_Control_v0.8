# SIGNAL MANAGER - TIMELINE VISUAL CON DRAG & DROP

**Fecha:** 2026-03-09 15:45:00  
**Proyecto:** LAMP DAQ Control v0.8  
**Tipo:** Mejora Mayor - Timeline Interactivo

---

## 🎯 OBJETIVO

Transformar el Signal Manager de una visualización básica a un **timeline visual completo** con:
- Grid de canales dinámicos según dispositivo DAQ
- Drag & drop interactivo desde Signal Library
- Visualización de eventos como bloques coloreados
- Detección automática de conflictos temporales

---

## 📋 REQUERIMIENTOS DEL USUARIO

1. **Grid completo de canales**: Cada fila = 1 canal I/O del dispositivo
2. **Canales preconfigurados**: Según perfil del dispositivo actual (PCIe-1824 o PCI-1735U)
3. **Drag & drop interactivo**: Arrastrar señales desde library → soltar en canal deseado
4. **Prevención de conflictos**: No permitir superposición de eventos

---

## 🏗️ ARQUITECTURA IMPLEMENTADA

### 1. Nuevos ViewModels

#### **TimelineChannelViewModel** (`UI\WPF\ViewModels\SignalManager\TimelineChannelViewModel.cs`)
```csharp
public class TimelineChannelViewModel : ViewModelBase
{
    public int ChannelNumber { get; set; }
    public string ChannelName { get; set; }
    public DeviceType DeviceType { get; set; }
    public ObservableCollection<TimelineEventViewModel> Events { get; set; }
    
    // Métodos clave
    public bool AddEvent(SignalEvent signalEvent, double totalDurationSeconds)
    public bool HasConflict(SignalEvent newEvent)
    public void ClearEvents()
}
```

**Responsabilidades:**
- Representa una fila/canal en el timeline
- Contiene todos los eventos asignados a ese canal
- Valida conflictos de tiempo antes de agregar eventos

#### **TimelineEventViewModel** (`UI\WPF\ViewModels\SignalManager\TimelineChannelViewModel.cs`)
```csharp
public class TimelineEventViewModel : ViewModelBase
{
    public SignalEvent SignalEvent { get; set; }
    public double LeftPosition { get; set; }     // Porcentaje (0-100)
    public double Width { get; set; }            // Porcentaje (0-100)
    public string DisplayText { get; set; }
    public string Color => SignalEvent.Color;
    
    // Método clave
    public void RecalculatePosition(double totalDurationSeconds)
}
```

**Responsabilidades:**
- Representa un evento individual en el timeline
- Calcula posición y ancho como porcentaje del timeline
- Proporciona información visual (color, texto)

### 2. Control Personalizado: TimelineControl

#### **TimelineControl.xaml** (`UI\WPF\Controls\TimelineControl.xaml`)

**Estructura:**
```
┌─────────────────────────────────────────────────────┐
│ Time Ruler (0s, 1s, 2s, 3s, ...)                   │
├──────────┬──────────────────────────────────────────┤
│ Channel  │ [Event 1] [Event 2]      [Event 3]      │
│ Labels   │                                          │
│          │ ════════════════════════════════════════ │
│ Ch 0     │     [━━━━━━━]                            │
│ Analog   │                                          │
│ ─────────┼──────────────────────────────────────────┤
│ Ch 1     │                    [━━━━━━━━━━━]        │
│ Analog   │                                          │
│ ─────────┼──────────────────────────────────────────┤
│ Ch 2     │  [━━━━]   [━━━━]                         │
│ Analog   │                                          │
│ ─────────┼──────────────────────────────────────────┤
│ ...      │                                          │
└──────────┴──────────────────────────────────────────┘
```

**Características:**
- **Columna izquierda (120px):** Etiquetas de canales (nombre + tipo)
- **Columna derecha (*):** Área de timeline con eventos
- **Fila superior (30px):** Regla de tiempo con marcadores
- **AllowDrop=True:** Cada fila acepta drag & drop

#### **TimelineControl.xaml.cs** (`UI\WPF\Controls\TimelineControl.xaml.cs`)

**Métodos clave:**
```csharp
private void DrawTimeRuler()
{
    // Dibuja marcadores de tiempo cada segundo
    // Actualiza según TotalDurationSeconds
}

private void OnChannelDragOver(object sender, DragEventArgs e)
{
    // Valida si el objeto arrastrado es SignalEvent
    // Muestra cursor de "copy" si válido
}

private void OnChannelDrop(object sender, DragEventArgs e)
{
    // Calcula tiempo de drop según posición del mouse
    // Llama a viewModel.AddSignalToChannel(...)
    // Muestra error si hay conflicto
}
```

### 3. Mejoras en SignalManagerViewModel

#### **Inicialización Dinámica de Canales**
```csharp
private void InitializeTimelineChannels()
{
    TimelineChannels.Clear();

    if (_daqController == null || !_daqController.IsInitialized)
    {
        // Default: 8 canales si no hay dispositivo
        for (int i = 0; i < 8; i++)
            TimelineChannels.Add(new TimelineChannelViewModel(i, DeviceType.Analog));
        return;
    }

    // Obtener canales del dispositivo real
    int channelCount = _daqController.ChannelCount;
    
    // Detectar tipo según modelo
    var deviceModel = _daqController.DeviceModel ?? "";
    var deviceType = deviceModel.Contains("1735") 
        ? DeviceType.Digital 
        : DeviceType.Analog;

    // Crear canales dinámicamente
    for (int i = 0; i < channelCount; i++)
        TimelineChannels.Add(new TimelineChannelViewModel(i, deviceType));
}
```

**Resultado:**
- **PCIe-1824:** 32 canales analógicos (0-10V)
- **PCI-1735U:** 32 canales digitales (4 puertos × 8 bits)

#### **Método AddSignalToChannel**
```csharp
public bool AddSignalToChannel(SignalEvent templateEvent, int channelNumber, TimeSpan startTime)
{
    // 1. Validar que haya secuencia seleccionada
    if (SelectedSequence == null) return false;

    // 2. Obtener canal destino
    var channel = TimelineChannels.FirstOrDefault(c => c.ChannelNumber == channelNumber);
    if (channel == null) return false;

    // 3. Crear evento desde template
    var newEvent = new SignalEvent
    {
        EventId = Guid.NewGuid().ToString(),
        Name = templateEvent.Name,
        StartTime = startTime,
        Duration = templateEvent.Duration,
        Channel = channelNumber,
        DeviceType = channel.DeviceType,
        EventType = templateEvent.EventType,
        Parameters = new Dictionary<string, double>(templateEvent.Parameters),
        Description = templateEvent.Description,
        Color = templateEvent.Color
    };

    // 4. Verificar conflictos
    if (channel.HasConflict(newEvent))
    {
        MessageBox.Show(
            $"Cannot add event at {startTime.TotalSeconds:F1}s on channel {channelNumber}.\\n\\n" +
            "There is already an event in that time range.",
            "Time Conflict",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    // 5. Agregar a secuencia y timeline
    _sequenceEngine.AddEvent(SelectedSequence.SequenceId, newEvent);
    channel.AddEvent(newEvent, SelectedSequence.TotalDuration.TotalSeconds);
    
    StatusText = $"Added {newEvent.Name} to channel {channelNumber} at {startTime.TotalSeconds:F1}s";
    return true;
}
```

### 4. Signal Library con Drag & Drop

**Modificación en SignalManagerView.xaml:**
```xaml
<HierarchicalDataTemplate.ItemTemplate>
    <DataTemplate>
        <Border Background="#F5F5F5" 
                Padding="5,3" 
                Margin="2" 
                CornerRadius="3"
                Cursor="Hand"
                MouseDown="OnSignalMouseDown">
            <TextBlock Text="{Binding Name}" Tag="{Binding}"/>
        </Border>
    </DataTemplate>
</HierarchicalDataTemplate.ItemTemplate>
```

**Event Handler (SignalManagerView.xaml.cs):**
```csharp
private void OnSignalMouseDown(object sender, MouseButtonEventArgs e)
{
    if (e.LeftButton != MouseButtonState.Pressed) return;

    var border = sender as Border;
    if (border == null) return;

    var textBlock = border.Child as TextBlock;
    if (textBlock?.Tag is SignalEvent signalEvent)
    {
        // Iniciar operación drag & drop
        DragDrop.DoDragDrop(border, signalEvent, DragDropEffects.Copy);
    }
}
```

### 5. Converter para Renderizado

**PercentageToPixelConverter** (`UI\WPF\Converters\PercentageToPixelConverter.cs`)
```csharp
public class PercentageToPixelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2) return 0.0;
        
        double percentage = (double)values[0];  // 0-100
        double totalWidth = (double)values[1];  // Ancho total en píxeles
        
        return (percentage / 100.0) * totalWidth;
    }
}
```

**Uso en XAML:**
```xaml
<Border.Width>
    <MultiBinding Converter="{StaticResource PercentageToPixelConverter}">
        <Binding Path="Width"/>
        <Binding RelativeSource="{RelativeSource AncestorType=Border}" Path="ActualWidth"/>
    </MultiBinding>
</Border.Width>
```

---

## 🔄 FLUJO DE TRABAJO COMPLETO

### Caso de Uso: Agregar Señal DC 5V a Canal 0

1. **Usuario arrastra "DC 5V" desde Signal Library**
   - `OnSignalMouseDown` captura evento
   - `DragDrop.DoDragDrop` inicia operación con `SignalEvent` como data

2. **Usuario suelta sobre Canal 0 en t=2.5s**
   - `OnChannelDragOver` valida que es `SignalEvent`
   - `OnChannelDrop` calcula:
     - `percentage = mouseX / channelWidth = 0.25`
     - `dropTime = 0.25 × 10s = 2.5s`
   
3. **Validación de Conflicto**
   - `TimelineChannelViewModel.HasConflict()` verifica:
     ```csharp
     foreach (var existing in Events)
     {
         if (newStart < existingEnd && newEnd > existingStart)
             return true;  // Conflicto detectado
     }
     ```
   - Si no hay conflicto → continuar

4. **Agregar Evento**
   - `SignalManagerViewModel.AddSignalToChannel()` crea nuevo `SignalEvent`
   - `_sequenceEngine.AddEvent()` agrega a secuencia
   - `channel.AddEvent()` crea `TimelineEventViewModel`

5. **Renderizado Visual**
   - `TimelineEventViewModel` calcula:
     - `LeftPosition = (2.5s / 10s) × 100 = 25%`
     - `Width = (1s / 10s) × 100 = 10%`
   - Converter traduce a píxeles
   - Bloque coloreado aparece en timeline

---

## 📊 DETECCIÓN DE CONFLICTOS

### Algoritmo de Superposición
```csharp
public bool HasConflict(SignalEvent newEvent)
{
    var newStart = newEvent.StartTime.TotalSeconds;
    var newEnd = (newEvent.StartTime + newEvent.Duration).TotalSeconds;

    foreach (var existingEvent in Events)
    {
        var existingStart = existingEvent.SignalEvent.StartTime.TotalSeconds;
        var existingEnd = (existingEvent.SignalEvent.StartTime + 
                           existingEvent.SignalEvent.Duration).TotalSeconds;

        // Verifica si hay solapamiento
        if (newStart < existingEnd && newEnd > existingStart)
        {
            return true;  // ¡Conflicto!
        }
    }

    return false;  // Sin conflictos
}
```

### Casos de Conflicto

**Caso 1: Superposición Total**
```
Existente: [━━━━━━━━━━━]  (2s - 5s)
Nuevo:       [━━━━━]       (3s - 4s)
Resultado: ❌ CONFLICTO
```

**Caso 2: Superposición Parcial Izquierda**
```
Existente:     [━━━━━━━━━━━]  (3s - 6s)
Nuevo:     [━━━━━]            (1s - 4s)
Resultado: ❌ CONFLICTO
```

**Caso 3: Superposición Parcial Derecha**
```
Existente: [━━━━━━━━━━━]      (1s - 4s)
Nuevo:              [━━━━━]   (3s - 5s)
Resultado: ❌ CONFLICTO
```

**Caso 4: Sin Superposición**
```
Existente: [━━━━━]              (1s - 2s)
Nuevo:              [━━━━━]     (3s - 4s)
Resultado: ✅ OK
```

---

## 📦 ARCHIVOS MODIFICADOS/CREADOS

### Archivos Nuevos (5 archivos)

1. **`UI\WPF\ViewModels\SignalManager\TimelineChannelViewModel.cs`** (165 líneas)
   - `TimelineChannelViewModel` class
   - `TimelineEventViewModel` class

2. **`UI\WPF\Controls\TimelineControl.xaml`** (130 líneas)
   - UserControl con grid de canales
   - Drag & drop zones
   - Renderizado de eventos

3. **`UI\WPF\Controls\TimelineControl.xaml.cs`** (115 líneas)
   - Event handlers para drag & drop
   - DrawTimeRuler method
   - Drop calculation logic

4. **`UI\WPF\Converters\PercentageToPixelConverter.cs`** (26 líneas)
   - Converter para traducir % → pixels

5. **`Resources\SignalLibrary\Example_Laser_Alignment.json`** (55 líneas)
   - Secuencia de ejemplo con 3 eventos

### Archivos Modificados (4 archivos)

1. **`UI\WPF\ViewModels\SignalManager\SignalManagerViewModel.cs`**
   - `InitializeTimelineChannels()` - Obtiene canales de DAQController
   - `AddSignalToChannel()` - Agrega señal con validación
   - `UpdateTimeline()` - Actualiza visualización
   - Eliminada clase `TimelineChannelViewModel` duplicada

2. **`UI\WPF\Views\SignalManager\SignalManagerView.xaml`**
   - Agregado namespace `controls`
   - Signal Library items ahora arrastrables (MouseDown event)
   - Timeline reemplazado con `<controls:TimelineControl/>`

3. **`UI\WPF\Views\SignalManager\SignalManagerView.xaml.cs`**
   - `OnSignalMouseDown()` - Handler para iniciar drag

4. **`LAMP_DAQ_Control_v0.8.csproj`**
   - Agregado `TimelineChannelViewModel.cs`
   - Agregado `TimelineControl.xaml` + `.cs`
   - Agregado `PercentageToPixelConverter.cs`

---

## 🎨 ESTILO VISUAL

### Colores de Eventos (según tipo)
- **DC:** `#4A90E2` (Azul)
- **Ramp:** `#50C878` (Verde)
- **Waveform:** `#FF6B6B` (Rojo claro)
- **Digital Pulse:** `#FFA500` (Naranja)
- **Digital State:** `#9B59B6` (Púrpura)

### Estilos de Canal
```xaml
<Style x:Key="ChannelRowStyle" TargetType="Border">
    <Setter Property="BorderBrush" Value="#DDD"/>
    <Setter Property="BorderThickness" Value="0,0,0,1"/>
    <Setter Property="Background" Value="White"/>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="#F0F8FF"/>  <!-- Highlight al hover -->
        </Trigger>
    </Style.Triggers>
</Style>
```

### Estilos de Evento
```xaml
<Style x:Key="EventBlockStyle" TargetType="Border">
    <Setter Property="CornerRadius" Value="3"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="BorderBrush" Value="#333"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="BorderThickness" Value="2"/>  <!-- Resaltar al hover -->
            <Setter Property="BorderBrush" Value="Black"/>
        </Trigger>
    </Style.Triggers>
</Style>
```

---

## 🔧 COMPILACIÓN Y DESPLIEGUE

### Build Info
```
Comando: MSBuild /t:Rebuild /p:Configuration=Release
Resultado: ✅ SUCCESS
Errores: 0
Warnings: 18 (código existente, no Signal Manager)
Tiempo: 1.18 segundos
Output: bin\Release\LAMP_DAQ_Control_v0.8.exe
```

### Archivos Generados
- `LAMP_DAQ_Control_v0.8.exe` (Release)
- `LAMP_DAQ_Control_v0.8.pdb`
- Perfiles XML copiados a output

---

## ✅ VERIFICACIÓN FUNCIONAL

### Pruebas Manuales Recomendadas

1. **Test de Canales Dinámicos**
   - Iniciar aplicación con PCIe-1824 → Verificar 32 canales analógicos
   - Cambiar a PCI-1735U → Verificar 32 canales digitales

2. **Test de Drag & Drop**
   - Crear nueva secuencia
   - Arrastrar "DC 5V" a Canal 0 en t=0s
   - Arrastrar "Ramp Slow Up" a Canal 0 en t=2s
   - Verificar ambos eventos visibles

3. **Test de Conflictos**
   - Agregar "DC 5V" (1s duración) en t=0s
   - Intentar agregar otro evento en t=0.5s
   - Verificar mensaje de error: "Time Conflict"

4. **Test de Múltiples Canales**
   - Agregar eventos diferentes en Ch 0, 1, 2
   - Verificar que no interfieren entre canales

5. **Test de Visualización**
   - Verificar colores según tipo de evento
   - Verificar ancho proporcional a duración
   - Verificar posición correcta en timeline

---

## 📈 BENEFICIOS IMPLEMENTADOS

### ✅ Para el Usuario
1. **Visualización inmediata** del timeline completo
2. **Edición intuitiva** con drag & drop
3. **Feedback instantáneo** de conflictos
4. **Configuración automática** según hardware

### ✅ Para el Sistema
1. **Arquitectura escalable** (fácil agregar features)
2. **Separación de responsabilidades** (MVVM)
3. **Reutilización** de componentes (Converter, Controls)
4. **Validación robusta** antes de modificar datos

---

## 🚀 PRÓXIMOS PASOS (Fase 3)

### Mejoras Propuestas

1. **Timeline Avanzado**
   - Zoom in/out con mouse wheel
   - Pan horizontal arrastrando timeline
   - Grid lines cada segundo
   - Snap to grid al soltar eventos

2. **Edición de Eventos**
   - Redimensionar eventos arrastrando bordes
   - Mover eventos dentro del mismo canal
   - Click derecho → menú contextual (delete, duplicate, edit)

3. **Validación Mejorada**
   - Verificar compatibilidad evento-canal (Analog vs Digital)
   - Advertir si parámetros exceden límites hardware
   - Previsualización de conflictos antes de soltar

4. **Exportación/Importación**
   - Guardar timeline como imagen PNG
   - Exportar secuencia a CSV
   - Importar desde formatos externos

---

## 📚 REFERENCIAS

- **Código Base:** `c:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\`
- **Documentación Anterior:** `SIGNAL_MANAGER_IMPLEMENTATION_2026-03-09_153500.md`
- **Ejemplo JSON:** `Resources\SignalLibrary\Example_Laser_Alignment.json`

---

**FIN DEL DOCUMENTO**
