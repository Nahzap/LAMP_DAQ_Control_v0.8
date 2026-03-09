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

                var width = TimeRulerCanvas.ActualWidth;
                if (width <= 0) width = 800;

                // Draw time markers every second
                for (int i = 0; i <= totalSeconds; i++)
                {
                    var x = (i / totalSeconds) * width;
                    
                    // Draw tick line
                    var line = new System.Windows.Shapes.Line
                    {
                        X1 = x,
                        Y1 = 20,
                        X2 = x,
                        Y2 = 30,
                        Stroke = Brushes.Gray,
                        StrokeThickness = 1
                    };
                    TimeRulerCanvas.Children.Add(line);

                    // Draw time label
                    var text = new TextBlock
                    {
                        Text = $"{i}s",
                        FontSize = 9,
                        Foreground = Brushes.Black
                    };
                    Canvas.SetLeft(text, x - 10);
                    Canvas.SetTop(text, 2);
                    TimeRulerCanvas.Children.Add(text);
                }
            }
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
