# GUÍA DE DEPLOYMENT Y CONFIGURACIÓN
## LAMP DAQ Control v0.8 - Documentación Técnica

**Fecha:** 2026-03-09  
**Documento:** Parte 4 de Auditoría Completa

---

## 📋 REQUISITOS DEL SISTEMA

### Hardware Requerido
- **CPU:** Intel/AMD x86/x64, mínimo 2 GHz
- **RAM:** Mínimo 4 GB, recomendado 8 GB
- **Disco:** 500 MB libres para instalación
- **Puertos:**
  - 1x PCIe x1 (para PCIe-1824)
  - 1x PCI 32-bit (para PCI-1735U)

### Software Requerido
- **Sistema Operativo:** Windows 7/8/10/11 (x86/x64)
- **.NET Framework:** 4.7.2 o superior
- **Advantech DAQNavi:** Versión 4.0.0.0 o superior
- **Visual Studio:** 2017 o superior (para desarrollo)
- **MSBuild:** 15.0 o superior (para compilación)

### Drivers y SDK
- **Advantech DAQNavi SDK**
  - Ubicación: `C:\Program Files\Advantech\DAQNavi\`
  - DLLs requeridas:
    - `Automation.BDaq4.dll`
    - `Automation.BDaq4.Design.dll`

---

## 🔧 INSTALACIÓN

### Paso 1: Instalación de Hardware

#### PCIe-1824 (Analógica)
1. Apagar el PC completamente
2. Instalar la tarjeta en slot PCIe x1 libre
3. Asegurar con tornillo
4. Conectar alimentación externa si es necesario
5. Configurar Board ID mediante DIP switches (default: 0)

#### PCI-1735U (Digital)
1. Apagar el PC completamente
2. Instalar la tarjeta en slot PCI 32-bit libre
3. Asegurar con tornillo
4. Configurar Board ID mediante jumpers (default: 1)

### Paso 2: Instalación de Drivers

1. **Descargar Advantech DAQNavi**
   - Sitio: https://www.advantech.com
   - Versión: 4.0.0.0 o superior

2. **Ejecutar instalador**
   ```
   DAQNavi_Setup_4.0.0.0.exe
   ```

3. **Seleccionar componentes:**
   - ✅ Runtime Libraries
   - ✅ .NET SDK
   - ✅ Device Drivers
   - ✅ Utility Software

4. **Verificar instalación:**
   ```cmd
   cd "C:\Program Files\Advantech\DAQNavi\Utility"
   DeviceManager.exe
   ```

5. **Configurar dispositivos en DeviceManager:**
   - Verificar que ambas tarjetas aparezcan
   - Asignar Board IDs correctos
   - Probar comunicación

### Paso 3: Instalación de .NET Framework

1. **Verificar versión instalada:**
   ```powershell
   Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP' -Recurse |
   Get-ItemProperty -Name Version -EA 0 |
   Where { $_.PSChildName -Match '^(?!S)\p{L}'} |
   Select PSChildName, Version
   ```

2. **Si es necesario, instalar .NET 4.7.2:**
   - Descargar de: https://dotnet.microsoft.com/download/dotnet-framework/net472
   - Ejecutar: `ndp472-kb4054530-x86-x64-allos-enu.exe`

### Paso 4: Compilación del Proyecto

#### Opción A: Script Automático (Recomendado)
```cmd
cd C:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8
BUILD.cmd
```

#### Opción B: MSBuild Manual
```powershell
# Buscar MSBuild
$msbuild = Get-ChildItem "C:\Program Files\" -Recurse -Filter "MSBuild.exe" | 
            Where-Object { $_.FullName -like "*\Current\Bin\MSBuild.exe" } | 
            Select-Object -First 1

# Compilar Release
& $msbuild.FullName "LAMP_DAQ_Control_v0.8.sln" /t:Rebuild /p:Configuration=Release

# Compilar Debug
& $msbuild.FullName "LAMP_DAQ_Control_v0.8.sln" /t:Rebuild /p:Configuration=Debug
```

#### Opción C: Visual Studio
1. Abrir `LAMP_DAQ_Control_v0.8.sln`
2. Seleccionar configuración (Debug/Release)
3. Build → Rebuild Solution (Ctrl+Shift+B)

**⚠️ IMPORTANTE:** NO usar `dotnet build` - no es compatible con WPF en .NET Framework.

### Paso 5: Verificar Compilación

```powershell
# Verificar ejecutable
Test-Path ".\bin\Release\LAMP_DAQ_Control_v0.8.exe"

# Verificar perfiles XML
Test-Path ".\bin\Release\Core\DAQ\Profiles\PCIe1824_prof_v1.xml"
Test-Path ".\bin\Release\Core\DAQ\Profiles\PCI1735U_prof_v1.xml"

# Verificar DLLs
Test-Path ".\bin\Release\Automation.BDaq4.dll"
```

---

## 🚀 EJECUCIÓN

### Modo WPF (Interfaz Gráfica)
```powershell
cd C:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\bin\Release
.\LAMP_DAQ_Control_v0.8.exe
```

### Modo Consola
```powershell
cd C:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\bin\Release
.\LAMP_DAQ_Control_v0.8.exe -console
```

### Desde Visual Studio
1. Establecer `LAMP_DAQ_Control_v0.8` como proyecto de inicio
2. Presionar F5 (Debug) o Ctrl+F5 (Run)

---

## ⚙️ CONFIGURACIÓN

### Perfiles de Dispositivos

#### PCIe1824_prof_v1.xml
**Ubicación:** `Core/DAQ/Profiles/PCIe1824_prof_v1.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<DAQNavi Version="4.0.0.0" Profile="2.0.0.0">
    <DaqDevice ModuleIndex="0">
        <Property ID="8">
            <Value>2110</Value>  <!-- Product ID -->
        </Property>
    </DaqDevice>
    <DaqAo ModuleIndex="0">
        <Property ID="52">
            <!-- Value Range por canal (1=0-10V, 33=0-20mA, 34=4-20mA) -->
            <Value>1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,33</Value>
        </Property>
    </DaqAo>
</DAQNavi>
```

**Modificaciones Comunes:**
- Cambiar rangos de voltaje por canal
- Configurar canales para corriente (4-20mA)

#### PCI1735U_prof_v1.xml
**Ubicación:** `Core/DAQ/Profiles/PCI1735U_prof_v1.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<DAQNavi Profile="2.0.0.0" Version="4.0.0.0">
    <DaqDio ModuleIndex="0">
        <Property ID="120">
            <!-- DI Data Mask (255 = todos los bits habilitados) -->
            <Value>255,255,255,255</Value>
        </Property>
        <Property ID="121">
            <!-- DO Data Mask (255 = todos los bits habilitados) -->
            <Value>255,255,255,255</Value>
        </Property>
        <Property ID="82">
            <!-- Ports Type (2 = Bidireccional) -->
            <Value>2,2,2,2</Value>
        </Property>
    </DaqDio>
</DAQNavi>
```

**Modificaciones Comunes:**
- Cambiar dirección de puertos (DI/DO)
- Habilitar/deshabilitar bits específicos

### Configuración de Board IDs

#### Hardware (DIP Switches/Jumpers)
**PCIe-1824:**
- Ubicación: DIP switches en la tarjeta
- Valores: 0-7
- Default: 0

**PCI-1735U:**
- Ubicación: Jumpers JP1-JP3
- Valores: 0-7
- Default: 1

#### Software (Advantech DeviceManager)
1. Abrir DeviceManager
2. Seleccionar dispositivo
3. Properties → Board ID
4. Aplicar cambios
5. Reiniciar PC

### Variables de Configuración en Código

#### DigitalInputMonitor.cs
```csharp
// Frecuencia de monitoreo (línea 40)
public void StartMonitoring(int deviceNumber, int intervalMs = 50)
{
    // intervalMs: 10-1000 ms
    // Default: 50ms (20 Hz)
    // Recomendado: 100ms (10 Hz) para bajo CPU
}
```

#### SignalGenerator.cs
```csharp
// Tasa de muestreo para señales (línea 298)
const double sampleRate = 1000000.0; // 1 MHz
// Modificar para ajustar precisión vs CPU usage
```

#### LogViewModel.cs
```csharp
// Límite de mensajes en consola (línea 35)
while (LogEntries.Count > 500)
{
    LogEntries.RemoveAt(0);
}
// Modificar para más/menos historial
```

---

## 🔍 VERIFICACIÓN POST-INSTALACIÓN

### Test 1: Detección de Dispositivos
```csharp
// En modo consola o WPF
var controller = new DAQController();
var devices = controller.DetectDevices();

// Debe mostrar:
// - PCIe-1824 (ID: 0) - 32 canales
// - PCI-1735U (ID: 1) - 32 canales
```

### Test 2: Escritura Analógica
```csharp
controller.Initialize("PCIe1824_prof_v1", 0);
controller.WriteVoltage(0, 5.0); // 5V en canal 0
// Verificar con multímetro en terminal de salida
```

### Test 3: Lectura Digital
```csharp
controller.Initialize("PCI1735U_prof_v1", 1);
byte value = controller.ReadDigitalPort(0);
// Verificar con señales de entrada
```

### Test 4: Escritura Digital
```csharp
controller.WriteDigitalPort(0, 0xFF); // Todos los bits HIGH
controller.WriteDigitalPort(0, 0x00); // Todos los bits LOW
// Verificar con LEDs o multímetro
```

---

## 🐛 TROUBLESHOOTING

### Problema: "No se detectaron dispositivos"

**Causas Posibles:**
1. Drivers no instalados
2. Tarjetas no reconocidas por Windows
3. Board IDs incorrectos

**Soluciones:**
```powershell
# 1. Verificar Device Manager de Windows
devmgmt.msc
# Buscar en "Data Acquisition Devices"

# 2. Reinstalar drivers Advantech
cd "C:\Program Files\Advantech\DAQNavi\Utility"
.\DeviceManager.exe

# 3. Verificar Board IDs
# Ejecutar Advantech DeviceManager y verificar configuración
```

### Problema: "Error al inicializar dispositivo"

**Causas Posibles:**
1. Perfil XML incorrecto
2. Dispositivo ocupado por otra aplicación
3. Permisos insuficientes

**Soluciones:**
```powershell
# 1. Verificar perfiles XML existen
Test-Path ".\Core\DAQ\Profiles\*.xml"

# 2. Cerrar otras aplicaciones DAQ
Get-Process | Where-Object {$_.ProcessName -like "*DAQ*"} | Stop-Process

# 3. Ejecutar como Administrador
Start-Process ".\LAMP_DAQ_Control_v0.8.exe" -Verb RunAs
```

### Problema: "Botones no responden en WPF"

**Causas Posibles:**
1. DataContext no configurado
2. Commands no bindeados
3. CanExecute retorna false

**Soluciones:**
```csharp
// Verificar en DEBUG_ANALOG.txt
// Debe mostrar: ">>> COMANDO INICIADO <<<"

// Si no aparece, verificar binding en XAML:
<Button Command="{Binding SetDcCommand}" />

// Verificar DataContext en MainWindow.xaml.cs:
this.DataContext = new MainViewModel();
```

### Problema: "CPU usage alto"

**Causas Posibles:**
1. Frecuencia de monitoreo muy alta
2. Múltiples señales generándose
3. Logging excesivo

**Soluciones:**
```csharp
// 1. Reducir frecuencia de monitoreo
_monitor.StartMonitoring(deviceNumber, 100); // 100ms = 10 Hz

// 2. Detener señales no usadas
controller.StopSignalGeneration();

// 3. Deshabilitar logging DEBUG
// En ConsoleLogger.cs, comentar línea 21
```

### Problema: "Jitter en señales generadas"

**Causas Posibles:**
1. Otros procesos consumiendo CPU
2. Thread priority no configurado
3. Antivirus interfiriendo

**Soluciones:**
```powershell
# 1. Cerrar aplicaciones innecesarias
# 2. Aumentar prioridad del proceso
$process = Get-Process "LAMP_DAQ_Control_v0.8"
$process.PriorityClass = "High"

# 3. Agregar excepción en antivirus
# Windows Defender → Exclusiones → Agregar carpeta
```

---

## 📊 MONITOREO Y LOGS

### Archivos de Log

#### DEBUG_ANALOG.txt
**Ubicación:** `C:\LAMP_CONTROL\LAMP_DAQ_Control_v0.8\DEBUG_ANALOG.txt`
**Contenido:** Debugging de comandos analógicos
```
11:45:30 - AnalogControlViewModel CONSTRUCTOR LLAMADO
11:45:35 - SetDc EJECUTADO - Voltage=5.0, Canal=0
```

#### Consola de Windows (ConsoleLogger)
**Formato:**
```
[INFO]  2026-03-09 11:45:30.123: Sistema iniciado
[DEBUG] 2026-03-09 11:45:30.456: Canal 0 configurado
[WARN]  2026-03-09 11:45:30.789: Frecuencia ajustada
[ERROR] 2026-03-09 11:45:31.012: Error de comunicación
```

#### Consola WPF (LogViewModel)
**Características:**
- Límite: 500 mensajes
- Niveles: Info, Success, Warning, Error
- Símbolos: ℹ ✓ ⚠️ ❌
- Colores: Negro, Verde, Naranja, Rojo

### Métricas de Performance

#### Monitoreo de CPU
```powershell
# PowerShell
while($true) {
    $cpu = (Get-Process "LAMP_DAQ_Control_v0.8").CPU
    Write-Host "CPU: $cpu seconds"
    Start-Sleep 1
}
```

#### Monitoreo de Memoria
```powershell
# PowerShell
while($true) {
    $mem = (Get-Process "LAMP_DAQ_Control_v0.8").WorkingSet64 / 1MB
    Write-Host "Memory: $([math]::Round($mem, 2)) MB"
    Start-Sleep 1
}
```

---

## 🔐 SEGURIDAD Y PERMISOS

### Permisos Requeridos
- **Lectura/Escritura:** Carpeta de instalación
- **Hardware Access:** Drivers Advantech
- **Network:** No requerido (aplicación standalone)

### Recomendaciones de Seguridad
1. ✅ Ejecutar con cuenta de usuario estándar
2. ✅ No exponer puertos de red
3. ✅ Mantener drivers actualizados
4. ✅ Backup de configuraciones
5. ⚠️ No ejecutar código no confiable

### Firewall
**No requiere configuración** - Aplicación no usa red

---

## 📦 DISTRIBUCIÓN

### Crear Paquete de Distribución

```powershell
# Script de empaquetado
$version = "0.8"
$outputDir = ".\Release_Package_v$version"

# Crear estructura
New-Item -ItemType Directory -Path $outputDir -Force
New-Item -ItemType Directory -Path "$outputDir\Core\DAQ\Profiles" -Force

# Copiar ejecutable y DLLs
Copy-Item ".\bin\Release\LAMP_DAQ_Control_v0.8.exe" $outputDir
Copy-Item ".\bin\Release\*.dll" $outputDir

# Copiar perfiles
Copy-Item ".\Core\DAQ\Profiles\*.xml" "$outputDir\Core\DAQ\Profiles\"

# Copiar documentación
Copy-Item ".\README.md" $outputDir
Copy-Item ".\LICENSE" $outputDir

# Crear ZIP
Compress-Archive -Path $outputDir -DestinationPath "LAMP_DAQ_Control_v$version.zip"
```

### Contenido del Paquete
```
LAMP_DAQ_Control_v0.8.zip
├── LAMP_DAQ_Control_v0.8.exe
├── Automation.BDaq4.dll
├── Automation.BDaq4.Design.dll
├── ScottPlot.WPF.dll
├── Common.Logging.dll
├── Core/
│   └── DAQ/
│       └── Profiles/
│           ├── PCIe1824_prof_v1.xml
│           └── PCI1735U_prof_v1.xml
├── README.md
└── LICENSE
```

---

## 🔄 ACTUALIZACIÓN

### Desde Versión Anterior

1. **Backup de configuración:**
   ```powershell
   Copy-Item ".\Core\DAQ\Profiles\*.xml" ".\Backup\"
   ```

2. **Detener aplicación:**
   ```powershell
   Stop-Process -Name "LAMP_DAQ_Control_v0.8" -Force
   ```

3. **Reemplazar archivos:**
   ```powershell
   Copy-Item ".\new_version\*" ".\current\" -Recurse -Force
   ```

4. **Restaurar configuración personalizada:**
   ```powershell
   Copy-Item ".\Backup\*.xml" ".\Core\DAQ\Profiles\" -Force
   ```

5. **Verificar:**
   ```powershell
   .\LAMP_DAQ_Control_v0.8.exe
   ```

---

## 📝 CHECKLIST DE DEPLOYMENT

### Pre-Deployment
- [ ] Hardware instalado correctamente
- [ ] Drivers Advantech instalados
- [ ] .NET Framework 4.7.2 instalado
- [ ] Board IDs configurados
- [ ] Dispositivos detectados en DeviceManager

### Compilación
- [ ] Código compilado sin errores
- [ ] Perfiles XML copiados a bin/
- [ ] DLLs de Advantech presentes
- [ ] Ejecutable funciona en modo Debug

### Testing
- [ ] Detección de dispositivos OK
- [ ] Escritura analógica OK
- [ ] Lectura digital OK
- [ ] Escritura digital OK
- [ ] Generación de señales OK
- [ ] Logging funcional

### Post-Deployment
- [ ] Documentación actualizada
- [ ] Backup de configuración
- [ ] Usuarios capacitados
- [ ] Soporte técnico disponible

---
