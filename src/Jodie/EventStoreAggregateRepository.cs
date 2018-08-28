using System.Collections.Generic;

namespace Jodie
{
    public class EventStoreAggregateRepository<TAggregate> : IAggregateRepository<TAggregate> where TAggregate : Aggregate, new()
    {
        private readonly IEventStore _eventStore;

        public EventStoreAggregateRepository(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public TAggregate Get(string id)
        {
            var events = _eventStore.LoadEvents(id) ?? new List<IEvent>();

            var application = new TAggregate();
            application.ApplyEvents(events);

            return application;
        }
    }
}