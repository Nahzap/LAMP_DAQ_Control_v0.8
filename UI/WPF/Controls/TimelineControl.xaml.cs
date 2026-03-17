using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private bool _isScrollingSynchronized = false;
        private System.Collections.Generic.HashSet<string> _activeEvents = new System.Collections.Generic.HashSet<string>();

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
                System.Console.WriteLine($"[TIMELINE] {e.PropertyName} changed, redrawing ruler and playhead");
                DrawTimeRuler();
                UpdatePlayhead(); // Redraw playhead on zoom/width change
            }
            else if (e.PropertyName == nameof(SignalManagerViewModel.CurrentTimeSeconds))
            {
                UpdatePlayhead();
            }
            else if (e.PropertyName == nameof(SignalManagerViewModel.ExecutionStateText))
            {
                // Show playhead during playback, hide when stopped
                var viewModel = DataContext as SignalManagerViewModel;
                if (viewModel != null)
                {
                    bool isPlaying = viewModel.ExecutionStateText != "Idle" && viewModel.ExecutionStateText != "Stopped";
                    var oldVisibility = PlayheadLine.Visibility;
                    PlayheadLine.Visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
                    System.Console.WriteLine($"[PLAYHEAD VISIBILITY] State: {viewModel.ExecutionStateText}, IsPlaying: {isPlaying}, Visibility: {oldVisibility} → {PlayheadLine.Visibility}");
                }
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

            // CRITICAL: Get mouse position BEFORE zoom to keep it centered
            Point mousePos = e.GetPosition(TimelineScrollViewer);
            double currentScroll = TimelineScrollViewer.HorizontalOffset;
            double currentWidth = viewModel.TimelineWidth;
            
            // Calculate the time position under the mouse (normalized 0-1)
            double mouseTimeRatio = (currentScroll + mousePos.X - 120) / currentWidth; // 120px = labels column
            if (mouseTimeRatio < 0) mouseTimeRatio = 0;
            if (mouseTimeRatio > 1) mouseTimeRatio = 1;
            
            System.Console.WriteLine($"[ZOOM BEFORE] Mouse at X={mousePos.X:F0}px, Scroll={currentScroll:F0}px, Width={currentWidth:F0}px, TimeRatio={mouseTimeRatio:F3}");

            // Zoom gradual: ±10% por cada paso
            double factor = e.Delta > 0 ? 1.1 : 0.9091; // 1/1.1 = 0.9091
            double oldZoom = viewModel.ZoomLevel;
            double newZoom = oldZoom * factor;
            
            // Limitar entre 0.1 y 100
            newZoom = Math.Max(0.1, Math.Min(100, newZoom));
            viewModel.ZoomLevel = newZoom;
            
            // CRITICAL: Adjust scroll to keep mouse position at same time
            // After zoom, the timeline width changes, so we need to recalculate scroll
            Dispatcher.BeginInvoke(new Action(() =>
            {
                double newWidth = viewModel.TimelineWidth;
                double targetScrollOffset = (mouseTimeRatio * newWidth) - (mousePos.X - 120);
                
                // Clamp to valid scroll range
                targetScrollOffset = Math.Max(0, Math.Min(TimelineScrollViewer.ScrollableWidth, targetScrollOffset));
                
                TimelineScrollViewer.ScrollToHorizontalOffset(targetScrollOffset);
                
                System.Console.WriteLine($"[ZOOM AFTER] NewWidth={newWidth:F0}px, TargetScroll={targetScrollOffset:F0}px (kept time ratio {mouseTimeRatio:F3})");
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            System.Console.WriteLine($"[ZOOM] Level: {oldZoom:F2}X → {newZoom:F2}X");
            
            e.Handled = true;
        }

        private void TimelineControl_Loaded(object sender, RoutedEventArgs e)
        {
            DrawTimeRuler();
            UpdatePlayhead();
        }

        /// <summary>
        /// Updates the playhead position based on current time
        /// </summary>
        private int _playheadUpdateCount = 0;
        
        private void UpdatePlayhead()
        {
            if (!(DataContext is SignalManagerViewModel viewModel))
            {
                return;
            }

            double currentTime = viewModel.CurrentTimeSeconds;
            double totalDuration = viewModel.TotalDurationSeconds;
            double timelineWidth = viewModel.TimelineWidth;

            if (totalDuration <= 0 || timelineWidth <= 0)
            {
                return;
            }

            double percentage = (currentTime / totalDuration) * 100.0;
            double x = (currentTime / totalDuration) * timelineWidth;

            double oldX1 = PlayheadLine.X1;
            PlayheadLine.X1 = x;
            PlayheadLine.X2 = x;
            PlayheadLine.Y1 = 0;
            PlayheadLine.Y2 = 10000;

            // Detect which events playhead is crossing
            DetectEventCrossing(viewModel, currentTime);

            // COMPRESSED LOGGING: Solo cada 10 updates (~300ms) para reducir spam
            _playheadUpdateCount++;
            if (_playheadUpdateCount % 10 == 0)
            {
                System.Console.WriteLine($"[PLAYHEAD] t={currentTime:F3}s ({percentage:F1}%) X={x:F1}px");
            }
        }

        /// <summary>
        /// Detects which events the playhead is currently crossing
        /// </summary>
        private void DetectEventCrossing(SignalManagerViewModel viewModel, double currentTime)
        {
            foreach (var channel in viewModel.TimelineChannels)
            {
                foreach (var eventVm in channel.Events)
                {
                    double eventStart = eventVm.SignalEvent.StartTime.TotalSeconds;
                    double eventEnd = (eventVm.SignalEvent.StartTime + eventVm.SignalEvent.Duration).TotalSeconds;

                    // Detect when playhead ENTERS an event (first frame inside)
                    if (currentTime >= eventStart && currentTime < eventStart + 0.1 && !_activeEvents.Contains(eventVm.SignalEvent.EventId))
                    {
                        _activeEvents.Add(eventVm.SignalEvent.EventId);
                        System.Console.WriteLine($"[PLAYHEAD ENTER] ▶ '{eventVm.SignalEvent.Name}' on {channel.ChannelName} @ {eventStart:F3}s");
                    }

                    // Detect when playhead EXITS an event (first frame after)
                    if (currentTime > eventEnd && _activeEvents.Contains(eventVm.SignalEvent.EventId))
                    {
                        _activeEvents.Remove(eventVm.SignalEvent.EventId);
                        System.Console.WriteLine($"[PLAYHEAD EXIT] ⏹ '{eventVm.SignalEvent.Name}' on {channel.ChannelName} @ {eventEnd:F3}s");
                    }
                }
            }
        }

        /// <summary>
        /// Synchronize ruler scroll when timeline scrolls horizontally
        /// </summary>
        private void OnTimelineScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isScrollingSynchronized) return;

            if (e.HorizontalChange != 0)
            {
                _isScrollingSynchronized = true;
                RulerScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
                _isScrollingSynchronized = false;
            }
        }

        /// <summary>
        /// Synchronize timeline scroll when ruler scrolls horizontally
        /// </summary>
        private void OnRulerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isScrollingSynchronized) return;

            if (e.HorizontalChange != 0)
            {
                _isScrollingSynchronized = true;
                TimelineScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
                _isScrollingSynchronized = false;
            }
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

            System.Console.WriteLine($"[GRID] Total: {totalNanoseconds}ns, Width: {width}px, ns/px: {nanosecondsPerPixel:F2}, Interval: {intervalNs}ns ({intervalNs / 1e9:F2}s)");

            // CRITICAL FIX: Dibujar marcador en tiempo 0 SIEMPRE, destacado en rojo
            var zeroLine = new System.Windows.Shapes.Line
            {
                X1 = 0,
                Y1 = 12,
                X2 = 0,
                Y2 = 30,
                Stroke = Brushes.Red,
                StrokeThickness = 2
            };
            TimeRulerCanvas.Children.Add(zeroLine);
            System.Console.WriteLine($"[RULER] Zero marker drawn at Canvas X=0 (TimeRulerCanvas width={width}px)");

            var zeroText = new TextBlock
            {
                Text = "0.0s",
                FontSize = 9,
                Foreground = Brushes.Red,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(zeroText, 2);
            Canvas.SetTop(zeroText, 0);
            TimeRulerCanvas.Children.Add(zeroText);

            // Dibujar resto de marcadores DESDE subIntervalNs (no desde 0, ya dibujado)
            for (long t = subIntervalNs; t <= totalNanoseconds; t += subIntervalNs)
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
            // MEJORADO: Objetivo 60-80px entre marcadores principales (más denso y legible)
            double targetNanoseconds = nanosecondsPerPixel * 70;
            
            // Intervalos más granulares para mejor UX
            long[] intervals = { 
                // Sub-milisegundo (para zoom muy alto)
                100_000, 200_000, 500_000,           // 0.1ms, 0.2ms, 0.5ms
                1_000_000, 2_000_000, 5_000_000,     // 1ms, 2ms, 5ms
                10_000_000, 20_000_000, 50_000_000,  // 10ms, 20ms, 50ms
                100_000_000, 200_000_000, 500_000_000, // 0.1s, 0.2s, 0.5s
                
                // Segundos (uso típico)
                1_000_000_000,                       // 1s
                2_000_000_000,                       // 2s
                5_000_000_000,                       // 5s
                10_000_000_000,                      // 10s
                30_000_000_000,                      // 30s
                60_000_000_000                       // 1min
            };
            
            foreach (var interval in intervals)
            {
                if (interval >= targetNanoseconds)
                    return interval;
            }
            return 60_000_000_000; // 1 minuto máximo
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
            // Accept both Copy (from library) and Move (repositioning)
            if (e.Data.GetDataPresent(typeof(SignalEvent)))
            {
                bool isExistingEvent = e.Data.GetDataPresent("IsExistingEvent") && 
                                       (bool)e.Data.GetData("IsExistingEvent");
                
                // Set appropriate effect
                e.Effects = isExistingEvent ? DragDropEffects.Move : DragDropEffects.Copy;
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

            var signalEvent = e.Data.GetData(typeof(SignalEvent)) as SignalEvent;
            if (signalEvent == null)
            {
                System.Console.WriteLine($"[DROP ERROR] Failed to cast data to SignalEvent");
                return;
            }

            bool isExistingEvent = e.Data.GetDataPresent("IsExistingEvent") && 
                                   (bool)e.Data.GetData("IsExistingEvent");

            System.Console.WriteLine($"[DROP] Signal: {signalEvent.Name}, Type: {signalEvent.EventType}, IsExisting: {isExistingEvent}");
            System.Console.WriteLine($"[DROP] EventId: {signalEvent.EventId}, Channel: {signalEvent.Channel}, Device: {signalEvent.DeviceModel}");

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

            var viewModel = DataContext as SignalManagerViewModel;
            if (viewModel == null)
            {
                System.Console.WriteLine($"[DROP ERROR] ViewModel not found");
                return;
            }

            // Calculate drop position
            Point dropPosition = e.GetPosition(border);
            
            // CRITICAL: Use TimelineWidth instead of ActualWidth for consistency with time markers
            double timelineWidth = viewModel.TimelineWidth;
            double dropPercentage = dropPosition.X / timelineWidth;
            
            System.Console.WriteLine($"[DROP] Drop position: X={dropPosition.X:F1}, Border.ActualWidth={border.ActualWidth:F0}, TimelineWidth={timelineWidth:F0}, Percentage={dropPercentage:F2}");

            // Calculate start time based on drop position
            double totalDurationSeconds = viewModel.TotalDurationSeconds;
            double rawStartSeconds = dropPercentage * totalDurationSeconds;
            
            // CRITICAL: Snap to grid (0.1s = 100ms intervals for precise alignment)
            double snapInterval = 0.1; // 100ms grid
            double snappedStartSeconds = Math.Round(rawStartSeconds / snapInterval) * snapInterval;
            
            TimeSpan startTime = TimeSpan.FromSeconds(snappedStartSeconds);
            System.Console.WriteLine($"[DROP] Raw: {rawStartSeconds:F3}s → Snapped: {snappedStartSeconds:F3}s (Grid: {snapInterval}s)");

            if (isExistingEvent)
            {
                // MOVE existing event to new channel
                System.Console.WriteLine($"[DROP] Moving existing event to new channel");
                var result = viewModel.MoveEventToChannel(signalEvent, channel, startTime);
                System.Console.WriteLine($"[DROP] MoveEventToChannel result: {result}");
            }
            else
            {
                // ADD new event from library
                System.Console.WriteLine($"[DROP] Calling AddSignalToChannel...");
                var result = viewModel.AddSignalToChannel(signalEvent, channel, startTime);
                System.Console.WriteLine($"[DROP] AddSignalToChannel result: {result}");
            }

            e.Handled = true;
        }

        private Point _eventDragStartPoint;
        private TimelineEventViewModel _draggedEvent;

        private void OnEventClick(object sender, MouseButtonEventArgs e)
        {
            System.Console.WriteLine($"[EVENT CLICK] OnEventClick called");
            
            if (sender is Border border && border.Tag is TimelineEventViewModel eventVM)
            {
                System.Console.WriteLine($"[EVENT CLICK] Event: {eventVM.SignalEvent.Name}, Type: {eventVM.SignalEvent.EventType}, Device: {eventVM.SignalEvent.DeviceType}");
                System.Console.WriteLine($"[EVENT CLICK] Setting SelectedTimelineEvent in ViewModel");
                
                var viewModel = DataContext as SignalManagerViewModel;
                if (viewModel != null)
                {
                    viewModel.SelectedTimelineEvent = eventVM;
                    System.Console.WriteLine($"[EVENT CLICK] SelectedTimelineEvent updated successfully");
                }
                
                // Store start point for potential drag
                _eventDragStartPoint = e.GetPosition(null);
                _draggedEvent = eventVM;
            }
        }

        private void OnEventMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedEvent != null)
            {
                try
                {
                    Point currentPosition = e.GetPosition(null);
                    Vector diff = _eventDragStartPoint - currentPosition;

                    // Only start drag if mouse moved enough (avoid accidental drags on click)
                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        System.Console.WriteLine($"[EVENT DRAG] Starting drag for: {_draggedEvent.SignalEvent.Name}");
                        System.Console.WriteLine($"[EVENT DRAG] From: Channel {_draggedEvent.SignalEvent.Channel}, Start {_draggedEvent.SignalEvent.StartTime.TotalSeconds:F3}s");
                        
                        // Create data object with existing event
                        var dataObject = new DataObject(typeof(SignalEvent), _draggedEvent.SignalEvent);
                        dataObject.SetData("IsExistingEvent", true);
                        
                        // Store reference before clearing to avoid null reference
                        var draggedEventRef = _draggedEvent;
                        _draggedEvent = null; // Clear BEFORE DoDragDrop to avoid re-entry
                        
                        System.Console.WriteLine($"[EVENT DRAG] Initiating DoDragDrop with Move effect...");
                        var result = DragDrop.DoDragDrop((DependencyObject)sender, dataObject, DragDropEffects.Move);
                        System.Console.WriteLine($"[EVENT DRAG] DoDragDrop completed. Result: {result}");
                        
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[EVENT DRAG ERROR] Exception during drag: {ex.Message}");
                    System.Console.WriteLine($"[EVENT DRAG ERROR] Stack trace: {ex.StackTrace}");
                    _draggedEvent = null; // Clean up on error
                    
                    MessageBox.Show(
                        $"Error during drag operation: {ex.Message}\n\nPlease try again.",
                        "Drag Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OnEventRightClick(object sender, MouseButtonEventArgs e)
        {
            System.Console.WriteLine($"[EVENT RIGHT CLICK] Context menu opening");
            
            if (sender is Border border && border.Tag is TimelineEventViewModel eventVM)
            {
                var viewModel = DataContext as SignalManagerViewModel;
                if (viewModel != null)
                {
                    viewModel.SelectedTimelineEvent = eventVM;
                    System.Console.WriteLine($"[EVENT RIGHT CLICK] Selected event: {eventVM.SignalEvent.Name}");
                }
            }
        }

        private void OnEditEvent(object sender, RoutedEventArgs e)
        {
            System.Console.WriteLine($"[EDIT EVENT] OnEditEvent called");
            
            var viewModel = DataContext as SignalManagerViewModel;
            if (viewModel?.SelectedTimelineEvent == null)
            {
                System.Console.WriteLine($"[EDIT EVENT ERROR] No event selected");
                return;
            }

            var selectedEvent = viewModel.SelectedTimelineEvent.SignalEvent;
            System.Console.WriteLine($"[EDIT EVENT] Editing: {selectedEvent.Name} on CH{selectedEvent.Channel}");
            
            // TODO: Open edit dialog - por ahora mostramos propiedades actuales
            string paramInfo = string.Join(", ", selectedEvent.Parameters.Select(kvp => $"{kvp.Key}={kvp.Value:F2}"));
            MessageBox.Show(
                $"Event: {selectedEvent.Name}\n" +
                $"Type: {selectedEvent.EventType}\n" +
                $"Channel: {selectedEvent.Channel}\n" +
                $"Start: {selectedEvent.StartTime.TotalSeconds:F3}s\n" +
                $"Duration: {selectedEvent.Duration.TotalSeconds:F3}s\n" +
                $"Device: {selectedEvent.DeviceModel}\n" +
                $"Parameters: {paramInfo}\n\n" +
                "Full edit dialog coming soon!",
                "Event Properties",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void OnDeleteEvent(object sender, RoutedEventArgs e)
        {
            System.Console.WriteLine($"[DELETE EVENT] OnDeleteEvent called");
            
            var viewModel = DataContext as SignalManagerViewModel;
            if (viewModel?.SelectedTimelineEvent == null)
            {
                System.Console.WriteLine($"[DELETE EVENT ERROR] No event selected");
                return;
            }

            var selectedEvent = viewModel.SelectedTimelineEvent.SignalEvent;
            System.Console.WriteLine($"[DELETE EVENT] Deleting: {selectedEvent.Name} (ID: {selectedEvent.EventId})");
            
            var result = MessageBox.Show(
                $"Are you sure you want to delete event '{selectedEvent.Name}'?\n\n" +
                $"Channel: {selectedEvent.DeviceModel} CH{selectedEvent.Channel}\n" +
                $"Time: {selectedEvent.StartTime.TotalSeconds:F1}s - {(selectedEvent.StartTime + selectedEvent.Duration).TotalSeconds:F1}s",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                bool success = viewModel.DeleteEvent(selectedEvent.EventId);
                System.Console.WriteLine($"[DELETE EVENT] Result: {(success ? "SUCCESS" : "FAILED")}");
                
                if (!success)
                {
                    MessageBox.Show(
                        "Failed to delete event. Please try again.",
                        "Delete Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OnDuplicateEvent(object sender, RoutedEventArgs e)
        {
            System.Console.WriteLine($"[DUPLICATE EVENT] OnDuplicateEvent called");
            
            var viewModel = DataContext as SignalManagerViewModel;
            if (viewModel?.SelectedTimelineEvent == null)
            {
                System.Console.WriteLine($"[DUPLICATE EVENT ERROR] No event selected");
                return;
            }

            var selectedEvent = viewModel.SelectedTimelineEvent.SignalEvent;
            System.Console.WriteLine($"[DUPLICATE EVENT] Duplicating: {selectedEvent.Name}");
            
            bool success = viewModel.DuplicateEvent(selectedEvent);
            System.Console.WriteLine($"[DUPLICATE EVENT] Result: {(success ? "SUCCESS" : "FAILED")}");
            
            if (!success)
            {
                MessageBox.Show(
                    "Failed to duplicate event. Check for time conflicts.",
                    "Duplicate Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
