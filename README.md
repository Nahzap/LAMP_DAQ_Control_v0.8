# LAMP DAQ Control v0.8

Controlador para tarjetas de adquisición de datos (DAQ) PCIe-1824 y PCI-1735U con interfaz visual WPF y monitoreo en tiempo real.

## 🎯 Descripción

Este software permite controlar las tarjetas de adquisición de datos Advantech PCIe-1824 y PCI-1735U desde una **interfaz visual moderna** o consola. Incluye funcionalidades para:

- **PCIe-1824**:
  - Generación de señales analógicas de alta precisión usando tablas de búsqueda (LUT)
  - Establecimiento de valores DC en canales analógicos
  - Realización de rampas de voltaje
  - Control de múltiples canales analógicos

- **PCI-1735U**:
  - Control de E/S digitales (32 canales en total)
  - Operaciones de lectura/escritura digital
  - Configuración de contadores programables
  - Control de temporización y conteo de eventos

## Características

### Para PCIe-1824
- **Salidas analógicas** de alta precisión
- **Generación de señales** con precisión mejorada mediante LUTs de 65536 puntos
- **Generación automática de LUT** si no existe en el directorio de salida
- **Optimización para reducir jitter** mediante acceso directo a CSV y ciclo completo
- **Configuración flexible** de parámetros de señal (frecuencia, amplitud, offset)
- **Soporte para múltiples canales** analógicos

### Para PCI-1735U
- **32 canales digitales** (4 puertos de 8 bits)
- **Operaciones de E/S digital** rápidas y confiables
- **3 contadores** para aplicaciones de temporización y conteo
- **Configuración flexible** de polaridad de señales

### Características Generales
- **Interfaz de consola** intuitiva y fácil de usar
- **Reinicio seguro** de todos los canales a 0V
- **Manejo de errores** robusto con mensajes descriptivos

## Requisitos del Sistema

- **Sistema Operativo**: Windows 7/8/10/11 (x86/x64)
- **.NET Framework**: 4.7.2 o superior
- **Hardware**: 
  - Tarjeta Advantech PCIe-1824 y/o PCI-1735U instaladas
  - Drivers de Advantech DAQNavi instalados
  - Alimentación adecuada para los canales analógicos y digitales

## 📦 Instalación

1. Asegúrese de tener instalados los drivers de **Advantech DAQNavi** para ambas tarjetas
2. Instale **.NET Framework 4.7.2** o superior si no está presente
3. Clone o descargue este repositorio
4. **Compile el proyecto** (ver sección Compilación)
5. Los archivos de perfil se copian automáticamente al directorio del ejecutable

## 🔨 Compilación

### Opción 1: Script de PowerShell (Recomendado)
```powershell
.\Build.ps1
```

### Opción 2: MSBuild Manual
```powershell
# Buscar MSBuild
$msbuild = Get-ChildItem "C:\Program Files\" -Recurse -Filter "MSBuild.exe" | 
            Where-Object { $_.FullName -like "*\Current\Bin\MSBuild.exe" } | 
            Select-Object -First 1

# Compilar
& $msbuild.FullName "LAMP_DAQ_Control_v0.8.sln" /t:Rebuild /p:Configuration=Release
```

**⚠️ IMPORTANTE:** NO use `dotnet build` - no es compatible con WPF en .NET Framework.

## 🚀 Uso

### Modo Visual (WPF) - Por defecto
```powershell
.\Run.ps1
# o directamente:
.\bin\Release\LAMP_DAQ_Control_v0.8.exe
```

### Modo Consola (Retrocompatible)
```powershell
.\Run.ps1 -Console
# o directamente:
.\bin\Release\LAMP_DAQ_Control_v0.8.exe -console
```

## 🎨 Características de la Interfaz Visual

### Panel de Control Analógico (PCIe-1824):

### Para PCIe-1824:
   - **1. Establecer valor DC**: Fija un voltaje constante en un canal
   - **2. Realizar rampa**: Ejecuta una rampa de voltaje en un canal
   - **3. Generar señal senoidal**: Inicia la generación de una señal senoidal
   - **4. Detener generación**: Detiene la señal generada actualmente

### Para PCI-1735U:
   - **1. Leer entrada digital**: Lee el estado de un canal de entrada
   - **2. Escribir salida digital**: Establece el estado de un canal de salida
   - **3. Configurar contador**: Configura los parámetros del contador
   - **4. Leer contador**: Lee el valor actual del contador

### Opciones Comunes:
   - **5. Mostrar información**: Muestra información del dispositivo DAQ
   - **6. Reiniciar canales**: Pone todos los canales a 0V o estado bajo
   - **7. Cambiar dispositivo**: Cambia entre los dispositivos disponibles
   - **8. Salir**: Cierra la aplicación

## Estructura del Proyecto

- **Core/**: Contiene las clases principales de la aplicación
  - **DAQ/**: Contiene los componentes modulares del sistema DAQ
    - **Interfaces/**: Interfaces para los componentes del sistema
    - **Managers/**: Gestores de dispositivos, perfiles y canales
      - `DeviceManager.cs`: Gestiona la detección e inicialización de dispositivos
      - `ProfileManager.cs`: Gestiona perfiles de configuración
      - `ChannelManager.cs`: Gestiona operaciones de canales
    - **Services/**: Servicios de generación de señales y utilidades
      - `SignalGenerator.cs`: Genera señales analógicas optimizadas (PCIe-1824)
      - `SignalLUT.cs`: Maneja la generación y acceso a tablas de búsqueda
  - `DAQController.cs`: Controlador principal que coordina los componentes
- **UI/**: Contiene la interfaz de usuario
  - `ConsoleUI.cs`: Implementa la interfaz de consola

## Configuración

Los siguientes archivos de configuración deben estar en el mismo directorio que el ejecutable:

- `PCIe1824_prof_v1.xml`: Configuración de la tarjeta PCIe-1824
- `PCI1735U_prof_v1.xml`: Configuración de la tarjeta PCI-1735U

## Solución de Problemas

### PCIe-1824
- **Error al cargar el perfil**: Verifique que el archivo `PCIe1824_prof_v1.xml` esté presente
- **Valores fuera de rango**: Los voltajes deben estar dentro del rango soportado (±10V)

### PCI-1735U
- **Error de comunicación**: Verifique que la tarjeta esté correctamente instalada
- **Canales no responden**: Verifique la configuración de E/S en `PCI1735U_prof_v1.xml`
- **Contadores no funcionan**: Verifique las conexiones de reloj y compuerta

### Problemas Comunes
- **Dispositivo no encontrado**: Asegúrese de que los drivers estén correctamente instalados
- **Error de permisos**: Ejecute la aplicación como administrador

## Licencia

Este software es propiedad de [Nombre de la Empresa/Institución]. Todos los derechos reservados.

## Contacto

Para soporte o preguntas, contacte a [correo de contacto].


## MMD:
graph TD
    %% Definición de estilos
    classDef entryPoint fill:#f9d71c,stroke:#333,stroke-width:2px
    classDef ui fill:#66ccff,stroke:#333,stroke-width:2px
    classDef core fill:#ff9966,stroke:#333,stroke-width:2px
    classDef interfaces fill:#99cc99,stroke:#333,stroke-width:2px
    classDef managers fill:#cc99ff,stroke:#333,stroke-width:2px
    classDef services fill:#ff99cc,stroke:#333,stroke-width:2px
    classDef models fill:#ffcc99,stroke:#333,stroke-width:2px
    classDef exceptions fill:#ff6666,stroke:#333,stroke-width:2px
    classDef external fill:#cccccc,stroke:#333,stroke-width:2px

    %% Capa de Entrada
    Program["Program.cs<br>(Entry Point)"]:::entryPoint
    
    %% Capa de Presentación
    ConsoleUI["ConsoleUI.cs<br>(UI Layer)"]:::ui
    
    %% Capa de Control
    DAQController["DAQController.cs<br>(Core Layer)"]:::core
    
    %% Capa de Abstracción (Interfaces)
    subgraph Interfaces["Interfaces Layer"]
        IDeviceManager["IDeviceManager"]:::interfaces
        IChannelManager["IChannelManager"]:::interfaces
        IProfileManager["IProfileManager"]:::interfaces
        ISignalGenerator["ISignalGenerator"]:::interfaces
        ILogger["ILogger"]:::interfaces
    end
    
    %% Capa de Implementación
    subgraph Managers["Managers Layer"]
        ProfileManager["ProfileManager"]:::managers
        ChannelManager["ChannelManager"]:::managers
        DeviceManager["DeviceManager"]:::managers
    end
    
    subgraph Services["Services Layer"]
        SignalGenerator["SignalGenerator"]:::services
        ConsoleLogger["ConsoleLogger"]:::services
    end
    
    %% Capa de Modelo
    subgraph Models["Models Layer"]
        ChannelState["ChannelState"]:::models
        DeviceInfo["DeviceInfo"]:::models
    end
    
    %% Capa de Excepciones
    subgraph Exceptions["Exceptions Layer"]
        DAQInitializationException["DAQInitializationException"]:::exceptions
    end
    
    %% Dependencias Externas
    AutomationBDaq["Automation.BDaq<br>(External Library)"]:::external
    
    %% Relaciones
    Program --> DAQController
    Program --> ConsoleUI
    ConsoleUI --> DAQController
    
    DAQController --> IDeviceManager
    DAQController --> IChannelManager
    DAQController --> IProfileManager
    DAQController --> ISignalGenerator
    DAQController --> ILogger
    
    IDeviceManager --> DeviceManager
    IChannelManager --> ChannelManager
    IProfileManager --> ProfileManager
    ISignalGenerator --> SignalGenerator
    ILogger --> ConsoleLogger
    
    DeviceManager --> AutomationBDaq
    SignalGenerator --> AutomationBDaq
    
    DeviceManager --> DeviceInfo
    ChannelManager --> ChannelState
    
    DAQController -.-> DAQInitializationException