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

            if (!(DataContext is SignalManagerViewModel viewModel))
                return;

            // PRECISIÓN INTERNA: nanosegundos
            long totalNanoseconds = viewModel.TotalDurationNanoseconds;
            if (totalNanoseconds <= 0) totalNanoseconds = 10_000_000_000; // 10s default

            var width = viewModel.TimelineWidth;
            if (width <= 0) width = 800;

            // Calcular qué representa cada pixel en nanosegundos
            double nanosecondsPerPixel = totalNanoseconds / width;
            
            // Determinar intervalo visual basado en zoom
            long intervalNs = CalculateIntervalNanoseconds(nanosecondsPerPixel);
            long subIntervalNs = intervalNs / 5; // 5 subdivisiones

            System.Console.WriteLine($"[GRID] Total: {totalNanoseconds}ns, Width: {width}px, ns/px: {nanosecondsPerPixel:F2}, Interval: {intervalNs}ns");

            // Dibujar marcadores desde 0 hasta totalNanoseconds
            for (long t = 0; t <= totalNanoseconds; t += subIntervalNs)
            {
                var x = ((double)t / totalNanoseconds) * width;
                bool isMajor = (t % intervalNs) == 0;
                
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
                        Text = FormatTimeNanoseconds(t),
                        FontSize = 9,
                        Foreground = Brushes.Black
                    };
                    Canvas.SetLeft(text, x - 15);
                    Canvas.SetTop(text, 0);
                    TimeRulerCanvas.Children.Add(text);
                }
            }
        }

        private long CalculateIntervalNanoseconds(double nanosecondsPerPixel)
        {
            // Objetivo: ~100 pixels entre marcadores principales
            double targetNanoseconds = nanosecondsPerPixel * 100;
            
            // Intervalos en nanosegundos (1ns, 2ns, 5ns, 10ns, ...)
            long[] intervals = { 
                1, 2, 5, 10, 20, 50, 100, 200, 500,  // nanosegundos
                1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000, 200_000, 500_000,  // microsegundos
                1_000_000, 2_000_000, 5_000_000, 10_000_000, 20_000_000, 50_000_000, 100_000_000, 200_000_000, 500_000_000,  // milisegundos
                1_000_000_000, 2_000_000_000, 5_000_000_000, 10_000_000_000  // segundos
            };
            
            foreach (var interval in intervals)
            {
                if (interval >= targetNanoseconds)
                    return interval;
            }
            return 10_000_000_000; // 10 segundos máximo
        }

        private string FormatTimeNanoseconds(long nanoseconds)
        {
            // Formato adaptativo según magnitud
            if (nanoseconds >= 1_000_000_000) 
                return $"{nanoseconds / 1_000_000_000.0:F1}s";
            if (nanoseconds >= 1_000_000) 
                return $"{nanoseconds / 1_000_000.0:F1}ms";
            if (nanoseconds >= 1_000) 
                return $"{nanoseconds / 1_000.0:F1}µs";
            return $"{nanoseconds}ns";
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
