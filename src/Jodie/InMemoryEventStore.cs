using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Jodie
{
    public class InMemoryEventStore : IEventStore
    {
        private readonly Dictionary<string, List<StoredEvent>> _events = new Dictionary<string, List<StoredEvent>>();

        public IEnumerable LoadEvents(string id)
        {
            var results = new List<IEvent>();

            if (_events.Any(s => s.Key == id) == false)
            {
                return results;
            }

            var aggEvents = _events[id];

            foreach (var e in aggEvents.OrderBy(se => se.Sequence))
            {
                var @event = DeserializeEvent(e.TypeName, e.Event);
                results.Add((IEvent) @event);
            }

            return results;
        }

        public void SaveAggregate(string aggregateId, Type aggregateType)
        {

        }

        public void SaveEvents(string id, IEnumerable<IEvent> newEvents, Type aggregateType)
        {
            var events = newEvents.ToList();

            var tempResults = new List<StoredEvent>();
            var eventsLoaded = GetAggregateEventCount(events.First().Id);

            const int idx = 1;

            foreach (var e in events)
            {
                var @event = SerializeEvent(e);

                tempResults.Add(new StoredEvent
                {
                    AggregateId = id,
                    Sequence = eventsLoaded + idx,
                    Event = @event,
                    TypeName = e.GetType().AssemblyQualifiedName
                });
            }

            if (_events.Any(s => s.Key == id) == false)
            {
                _events.Add(id, tempResults);
            }
            else
            {
                var loadedEvents = _events[id];
                tempResults.AddRange(loadedEvents);
                _events.Remove(id);
                _events.Add(id, tempResults);
            }
        }

        public int GetAggregateEventCount(string id)
        {
            return !_events.ContainsKey(id) ? 0 : _events[id].Max(e => e.Sequence);
        }
        
        private static string SerializeEvent(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        private static object DeserializeEvent(string typeName, string data)
        {
            return JsonConvert.DeserializeObject(data, Type.GetType(typeName));
        }
    }
}