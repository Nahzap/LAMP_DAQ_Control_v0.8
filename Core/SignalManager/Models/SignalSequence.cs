using System;
using System.Collections.Generic;
using System.Linq;

namespace LAMP_DAQ_Control_v0_8.Core.SignalManager.Models
{
    /// <summary>
    /// Represents a complete signal sequence containing multiple events
    /// </summary>
    public class SignalSequence
    {
        /// <summary>
        /// Unique identifier for this sequence
        /// </summary>
        public string SequenceId { get; set; }

        /// <summary>
        /// Name of this sequence
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of what this sequence does
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Version of this sequence
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Last modified timestamp
        /// </summary>
        public DateTime Modified { get; set; }

        /// <summary>
        /// Author/creator of this sequence
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// List of events in this sequence
        /// </summary>
        public List<SignalEvent> Events { get; set; }

        /// <summary>
        /// Additional metadata (tags, categories, etc.)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        public SignalSequence()
        {
            SequenceId = Guid.NewGuid().ToString();
            Events = new List<SignalEvent>();
            Metadata = new Dictionary<string, object>();
            Created = DateTime.Now;
            Modified = DateTime.Now;
            Version = "1.0";
        }

        /// <summary>
        /// Gets the total duration of this sequence
        /// </summary>
        public TimeSpan TotalDuration
        {
            get
            {
                if (Events == null || Events.Count == 0)
                    return TimeSpan.Zero;

                return Events.Max(e => e.EndTime);
            }
        }

        /// <summary>
        /// Gets events sorted by start time
        /// </summary>
        public List<SignalEvent> GetEventsSorted()
        {
            return Events.OrderBy(e => e.StartTime).ToList();
        }

        /// <summary>
        /// Gets events for a specific channel
        /// </summary>
        public List<SignalEvent> GetEventsForChannel(int channel)
        {
            return Events.Where(e => e.Channel == channel).OrderBy(e => e.StartTime).ToList();
        }

        /// <summary>
        /// Validates the entire sequence
        /// </summary>
        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("Sequence name is required.");
            }

            if (Events == null || Events.Count == 0)
            {
                errors.Add("Sequence must contain at least one event.");
                return false;
            }

            // Validate each event
            for (int i = 0; i < Events.Count; i++)
            {
                if (!Events[i].Validate(out string eventError))
                {
                    errors.Add($"Event {i} ({Events[i].Name}): {eventError}");
                }
            }

            // Check for overlapping events on same channel
            var channelGroups = Events.GroupBy(e => e.Channel);
            foreach (var group in channelGroups)
            {
                var channelEvents = group.OrderBy(e => e.StartTime).ToList();
                for (int i = 0; i < channelEvents.Count - 1; i++)
                {
                    var current = channelEvents[i];
                    var next = channelEvents[i + 1];

                    if (current.EndTime > next.StartTime)
                    {
                        errors.Add($"Overlap detected on channel {current.Channel}: " +
                                 $"Event '{current.Name}' ends at {current.EndTime.TotalSeconds:F3}s " +
                                 $"but '{next.Name}' starts at {next.StartTime.TotalSeconds:F3}s");
                    }
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Adds an event to the sequence
        /// </summary>
        public void AddEvent(SignalEvent evt)
        {
            if (evt == null)
                throw new ArgumentNullException(nameof(evt));

            Events.Add(evt);
            Modified = DateTime.Now;
        }

        /// <summary>
        /// Removes an event from the sequence
        /// </summary>
        public bool RemoveEvent(string eventId)
        {
            var evt = Events.FirstOrDefault(e => e.EventId == eventId);
            if (evt != null)
            {
                Events.Remove(evt);
                Modified = DateTime.Now;
                return true;
            }
            return false;
        }
    }
}
