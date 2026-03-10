using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LAMP_DAQ_Control_v0_8.Core.SignalManager.Models;
using LAMP_DAQ_Control_v0_8.UI.WPF.ViewModels.SignalManager;

namespace LAMP_DAQ_Control_v0_8.UI.WPF.Controls
{
    /// <summary>
    /// Interaction logic for TimelineControl.xaml
    /// </summary>
    public partial class TimelineControl : UserControl
    {
        public TimelineControl()
        {
            InitializeComponent();
            this.Loaded += TimelineControl_Loaded;
            this.DataContextChanged += TimelineControl_DataContextChanged;
        }

        private void TimelineControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SignalManagerViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is SignalManagerViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SignalManagerViewModel.ZoomLevel) ||
                e.PropertyName == nameof(SignalManagerViewModel.TimelineWidth))
            {
                DrawTimeRuler();
            }
        }

        private void TimelineControl_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            // Zoom horizontal solo con Ctrl + Mouse Wheel
            // Sin Ctrl = permitir scroll normal
            if (!System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
                return;

            var viewModel = DataContext as SignalManagerViewModel;
            if (viewModel == null) return;

            // Zoom gradual: ±10% por cada paso
            double factor = e.Delta > 0 ? 1.1 : 0.9091; // 1/1.1 = 0.9091
            double newZoom = viewModel.ZoomLevel * factor;
            
            // Limitar entre 0.1 y 100
            newZoom = Math.Max(0.1, Math.Min(100, newZoom));
            viewModel.ZoomLevel = newZoom;
            
            System.Console.WriteLine($"[ZOOM] Level: {newZoom:F2}X, Width: {viewModel.TimelineWidth:F0}px");
            
            e.Handled = true;
        }

        private void TimelineControl_Loaded(object sender, RoutedEventArgs e)
        {
            DrawTimeRuler();
        }

        private void DrawTimeRuler()
        {
            TimeRulerCanvas.Children.Clear();

            if (DataContext is SignalManagerViewModel viewModel && viewModel.SelectedSequence != null)
            {
                var totalSeconds = viewModel.TotalDurationSeconds;
                if (totalSeconds <= 0) totalSeconds = 10;

                var width = viewModel.TimelineWidth;
                if (width <= 0) width = 800;

                // Calcular intervalo basado en segundos por pixel
                double secondsPerPixel = totalSeconds / width;
                double interval = CalculateInterval(secondsPerPixel);
                double subInterval = interval / 5; // Subdivisiones menores

                // Dibujar marcadores principales y subdivisiones
                for (double t = 0; t <= totalSeconds; t += subInterval)
                {
                    var x = (t / totalSeconds) * width;
                    bool isMajor = Math.Abs(t % interval) < 0.0001;
                    
                    // Línea de marcador
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = x,
                        Y1 = isMajor ? 15 : 22,
                        X2 = x,
                        Y2 = 30,
                        Stroke = isMajor ? Brushes.Black : Brushes.LightGray,
                        StrokeThickness = isMajor ? 1.5 : 0.5
                    };
                    TimeRulerCanvas.Children.Add(line);

                    // Etiqueta solo en marcadores principales
                    if (isMajor)
                    {
                        var text = new TextBlock
                        {
                            Text = FormatTimeLabel(t),
                            FontSize = 9,
                            Foreground = Brushes.Black
                        };
                        Canvas.SetLeft(text, x - 15);
                        Canvas.SetTop(text, 0);
                        TimeRulerCanvas.Children.Add(text);
                    }
                }
            }
        }

        private double CalculateInterval(double secondsPerPixel)
        {
            // Objetivo: ~100 pixels entre marcadores principales
            double targetSeconds = secondsPerPixel * 100;
            
            // Encontrar intervalo apropiado
            double[] intervals = { 1e-9, 2e-9, 5e-9, 10e-9, 20e-9, 50e-9, 100e-9, 200e-9, 500e-9,
                                   1e-6, 2e-6, 5e-6, 10e-6, 20e-6, 50e-6, 100e-6, 200e-6, 500e-6,
                                   1e-3, 2e-3, 5e-3, 10e-3, 20e-3, 50e-3, 100e-3, 200e-3, 500e-3,
                                   1, 2, 5, 10, 20, 50, 100, 200, 500 };
            
            foreach (var interval in intervals)
            {
                if (interval >= targetSeconds)
                    return interval;
            }
            return 1000;
        }

        private string FormatTimeLabel(double seconds)
        {
            if (seconds >= 1) return $"{seconds:F0}s";
            if (seconds >= 0.001) return $"{seconds * 1000:F0}ms";
            if (seconds >= 0.000001) return $"{seconds * 1e6:F0}µs";
            return $"{seconds * 1e9:F0}ns";
        }

        private void OnChannelDragOver(object sender, DragEventArgs e)
        {
            // Reduce log spam - only log errors
            if (e.Data.GetDataPresent(typeof(SignalEvent)))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnChannelDrop(object sender, DragEventArgs e)
        {
            System.Console.WriteLine($"[DROP] OnChannelDrop called");
            
            if (!e.Data.GetDataPresent(typeof(SignalEvent)))
            {
                System.Console.WriteLine($"[DROP ERROR] No SignalEvent in drop data");
                return;
            }

            var templateEvent = e.Data.GetData(typeof(SignalEvent)) as SignalEvent;
            if (templateEvent == null)
            {
                System.Console.WriteLine($"[DROP ERROR] Failed to cast data to SignalEvent");
                return;
            }
            System.Console.WriteLine($"[DROP] Signal: {templateEvent.Name}, Type: {templateEvent.EventType}");

            var border = sender as Border;
            if (border == null)
            {
                System.Console.WriteLine($"[DROP ERROR] Sender is not a Border");
                return;
            }

            var channel = border.Tag as TimelineChannelViewModel;
            if (channel == null)
            {
                System.Console.WriteLine($"[DROP ERROR] Border.Tag is not TimelineChannelViewModel. Type: {border.Tag?.GetType().Name ?? "null"}");
                return;
            }
            System.Console.WriteLine($"[DROP] Target channel: {channel.ChannelName} (Device: {channel.DeviceModel}, CH: {channel.ChannelNumber})");

            // Calculate drop time based on mouse position
            var position = e.GetPosition(border);
            var percentage = position.X / border.ActualWidth;
            System.Console.WriteLine($"[DROP] Drop position: X={position.X}, Width={border.ActualWidth}, Percentage={percentage:F2}");
            
            var viewModel = DataContext as SignalManagerViewModel;
            if (viewModel == null)
            {
                System.Console.WriteLine($"[DROP ERROR] DataContext is not SignalManagerViewModel");
                return;
            }
            
            if (viewModel.SelectedSequence == null)
            {
                System.Console.WriteLine($"[DROP ERROR] No sequence selected");
                return;
            }

            var totalSeconds = viewModel.TotalDurationSeconds;
            if (totalSeconds <= 0) totalSeconds = 10;

            var dropTimeSeconds = percentage * totalSeconds;
            var startTime = TimeSpan.FromSeconds(Math.Max(0, dropTimeSeconds));
            System.Console.WriteLine($"[DROP] Calculated start time: {startTime.TotalSeconds:F2}s (Total duration: {totalSeconds}s)");

            // Add signal to channel
            System.Console.WriteLine($"[DROP] Calling AddSignalToChannel...");
            var success = viewModel.AddSignalToChannel(templateEvent, channel, startTime);
            System.Console.WriteLine($"[DROP] AddSignalToChannel result: {success}");

            e.Handled = true;
        }

        private void OnEventClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            System.Console.WriteLine($"[EVENT CLICK] OnEventClick called");
            
            var border = sender as Border;
            if (border == null)
            {
                System.Console.WriteLine($"[EVENT CLICK ERROR] Sender is not a Border");
                return;
            }

            var eventViewModel = border.Tag as TimelineEventViewModel;
            if (eventViewModel == null)
            {
                System.Console.WriteLine($"[EVENT CLICK ERROR] Border.Tag is not TimelineEventViewModel. Type: {border.Tag?.GetType().Name ?? "null"}");
                return;
            }

            var viewModel = DataContext as SignalManagerViewModel;
            if (viewModel == null)
            {
                System.Console.WriteLine($"[EVENT CLICK ERROR] DataContext is not SignalManagerViewModel");
                return;
            }

            System.Console.WriteLine($"[EVENT CLICK] Event: {eventViewModel.SignalEvent.Name}, Type: {eventViewModel.SignalEvent.EventType}, Device: {eventViewModel.SignalEvent.DeviceType}");
            System.Console.WriteLine($"[EVENT CLICK] Setting SelectedTimelineEvent in ViewModel");
            
            viewModel.SelectedTimelineEvent = eventViewModel;
            
            System.Console.WriteLine($"[EVENT CLICK] SelectedTimelineEvent updated successfully");
            e.Handled = true;
        }
    }
}
