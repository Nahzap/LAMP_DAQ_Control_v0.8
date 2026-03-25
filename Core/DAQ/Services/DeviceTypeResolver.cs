using System.Linq;
using LAMP_DAQ_Control_v0_8.Core.DAQ.Models;

namespace LAMP_DAQ_Control_v0_8.Core.DAQ.Services
{
    /// <summary>
    /// Fuente de verdad única para resolución de tipo de dispositivo a partir de nombres de perfil.
    /// Centraliza la lógica que antes estaba duplicada en DAQController y DeviceManager.
    /// </summary>
    public static class DeviceTypeResolver
    {
        private static readonly string[] DigitalIdentifiers = { "PCI1735", "1735" };
        private static readonly string[] AnalogIdentifiers = { "PCIe1824", "1824" };

        /// <summary>
        /// Determina el tipo de dispositivo basándose en el nombre del perfil.
        /// </summary>
        /// <param name="profileName">Nombre del perfil de configuración</param>
        /// <returns>DeviceType detectado (Analog, Digital, o Unknown)</returns>
        public static DeviceType ResolveFromProfile(string profileName)
        {
            if (string.IsNullOrEmpty(profileName))
                return DeviceType.Unknown;

            if (DigitalIdentifiers.Any(id => profileName.Contains(id)))
                return DeviceType.Digital;

            if (AnalogIdentifiers.Any(id => profileName.Contains(id)))
                return DeviceType.Analog;

            return DeviceType.Unknown;
        }

        /// <summary>
        /// Obtiene el perfil por defecto para un tipo de dispositivo dado.
        /// </summary>
        /// <param name="type">Tipo de dispositivo</param>
        /// <returns>Nombre del perfil por defecto, o null si no aplica</returns>
        public static string GetDefaultProfile(DeviceType type)
        {
            switch (type)
            {
                case DeviceType.Digital: return "PCI1735U_prof_v1";
                case DeviceType.Analog: return "PCIe1824_prof_v1";
                default: return null;
            }
        }

        /// <summary>
        /// Verifica si un perfil es digital.
        /// </summary>
        public static bool IsDigitalProfile(string profileName)
        {
            return ResolveFromProfile(profileName) == DeviceType.Digital;
        }

        /// <summary>
        /// Verifica si un perfil es analógico.
        /// </summary>
        public static bool IsAnalogProfile(string profileName)
        {
            return ResolveFromProfile(profileName) == DeviceType.Analog;
        }
    }
}
