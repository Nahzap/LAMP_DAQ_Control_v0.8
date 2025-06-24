# LAMP DAQ Control

Controlador para tarjetas de adquisición de datos (DAQ) PCIe-1824 y PCI-1735U, diseñado para generar señales de control para el sistema LAMP.

## Descripción

Este software permite controlar las tarjetas de adquisición de datos Advantech PCIe-1824 y PCI-1735U desde una interfaz de consola. Incluye funcionalidades para:

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
- **Generación de señales** con precisión mejorada mediante LUTs
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

## Instalación

1. Asegúrese de tener instalados los drivers de Advantech DAQNavi para ambas tarjetas
2. Instale el .NET Framework 4.7.2 o superior si no está presente
3. Clone o descargue este repositorio
4. Copie el contenido de la carpeta de instalación a la ubicación deseada
5. Asegúrese de que los archivos de perfil estén en el directorio del ejecutable:
   - `PCIe1824_prof_v1.xml` para la tarjeta PCIe-1824
   - `PCI1735U_prof_v1.xml` para la tarjeta PCI-1735U

## Uso

1. Ejecute `LAMP_DAQ_Control_v0.8.exe`
2. Seleccione el dispositivo a controlar (PCIe-1824 o PCI-1735U)
3. Seleccione una opción del menú principal:

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
  - `DAQController.cs`: Maneja la comunicación con las tarjetas DAQ
  - `SignalGenerator.cs`: Genera señales analógicas (PCIe-1824)
  - `DigitalIOManager.cs`: Maneja las E/S digitales (PCI-1735U)
  - `SignalLUT.cs`: Maneja las tablas de búsqueda para generación de señales
  - `ChannelConfig.cs`: Configuración de canales
- **UI/**: Contiene la interfaz de usuario
  - `ConsoleUI.cs`: Implementa la interfaz de consola
- **LUT/**: Directorio que contiene las tablas de búsqueda para generación de señales

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
