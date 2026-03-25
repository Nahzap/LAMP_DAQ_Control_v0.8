using System;
using System.Threading.Tasks;
using System.Windows;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Windows
{
    /// <summary>
    /// Pantalla de bienvenida autoexplicativa del sistema LAMP DAQ Control v0.8.
    /// Se muestra antes de la ventana principal para informar al usuario sobre
    /// las funcionalidades del programa mientras se inicializa el sistema.
    /// </summary>
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Actualiza el mensaje de estado y el porcentaje de la barra de progreso.
        /// </summary>
        /// <param name="message">Mensaje descriptivo del paso actual</param>
        /// <param name="percentage">Porcentaje de progreso (0-100)</param>
        public void UpdateProgress(string message, int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                LoadProgress.Value = percentage;
            });
        }

        /// <summary>
        /// Fuerza el procesamiento de mensajes WPF pendientes para que la UI se actualice.
        /// </summary>
        public void DoEvents()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new System.Windows.Threading.DispatcherOperationCallback(
                    delegate (object f)
                    {
                        ((System.Windows.Threading.DispatcherFrame)f).Continue = false;
                        return null;
                    }), frame);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        /// <summary>
        /// Muestra un paso de progreso y permite que la UI se renderice.
        /// </summary>
        /// <param name="message">Mensaje del paso</param>
        /// <param name="percentage">Porcentaje de progreso</param>
        /// <param name="delayMs">Tiempo mínimo que se muestra el paso (ms)</param>
        public void ShowStep(string message, int percentage, int delayMs = 200)
        {
            UpdateProgress(message, percentage);
            DoEvents();
            if (delayMs > 0)
            {
                System.Threading.Thread.Sleep(delayMs);
            }
        }
    }
}
