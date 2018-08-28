using System;
using System.Collections;
using System.Collections.Generic;

namespace Jodie
{
    public interface IEventStore
    {
        IEnumerable LoadEvents(string id);

        void SaveAggregate(string aggregateId, Type aggregateType);
        void SaveEvents(string id, IEnumerable<IEvent> newEvents, Type aggregateType);

        int GetAggregateEventCount(string id);
    }
}
