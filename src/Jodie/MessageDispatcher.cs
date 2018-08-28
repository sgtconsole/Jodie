using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jodie.Utility;
using Serilog;

namespace Jodie
{

    public class MessageDispatcher<T> : IMessageDispatcher where T : Aggregate
    {
        private readonly Dictionary<Type, Action<object>> _commandHandlers = new Dictionary<Type, Action<object>>();
        private readonly Dictionary<Type, List<Action<object>>> _eventSubscribers = new Dictionary<Type, List<Action<object>>>();
        private readonly Dictionary<Type, List<Action<object>>> _routers = new Dictionary<Type, List<Action<object>>>();
        private readonly IEventStore _eventStore;
        private readonly ILogger _logger;

        private readonly ConcurrentQueue<ICommand> _commandQueue = new ConcurrentQueue<ICommand>();
        private readonly ConcurrentQueue<IEvent> _eventQueue = new ConcurrentQueue<IEvent>();
        private Task _dispatcher;
        private CancellationTokenSource _dispatcherCancellationTokenSource;

        public MessageDispatcher(IEventStore eventStore, ILogger logger)
        {
            _eventStore = eventStore;
            _logger = logger;
            SingleThreadedMode = false;
        }

        public bool SingleThreadedMode { get; set; }

        public void Start()
        {
            _dispatcherCancellationTokenSource = new CancellationTokenSource();
            _dispatcher = new Task(DequeueMessages, _dispatcherCancellationTokenSource.Token);
            _dispatcher.Start();
        }

        public void Stop()
        {
            _dispatcherCancellationTokenSource.Cancel();
        }

        private void DequeueMessages()
        {
            while (!_dispatcherCancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var foundCommand = DequeueCommand();
                    var foundEvent = DequeueEvent();
                    
                    Thread.Sleep(foundCommand || foundEvent ? 0 : 500);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "An error occurred whilst dequeuing messages");
                    throw;
                }
            }
        }

        private bool DequeueCommand()
        {
            ICommand c;
            var foundCommand = _commandQueue.TryDequeue(out c);

            if (!foundCommand)
            {
                return false;
            }
         
            try
            {
                _commandHandlers[c.GetType()](c);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error occurred whilst executing command '{c.GetType().Name}' for aggregate id '{c.Id}'");
                return false;
            }
        }

        private bool DequeueEvent()
        {
            IEvent e;
            var foundEvent = _eventQueue.TryDequeue(out e);

            if (!foundEvent)
            {
                return false;
            }

            var eventTypeName = e.GetType().Name;

            try
            {
                _logger.Debug($"Event type '{eventTypeName}' saving for aggregate id '{e.Id}'");
                _eventStore.SaveEvents(e.Id, new List<IEvent> { e }, typeof(T));

                _logger.Debug($"Event type '{eventTypeName}' publishing for aggregate id '{e.Id}'");
                PublishEvent(e);

                _logger.Debug($"Event type '{eventTypeName}' routing for aggregate id '{e.Id}'");
                RouteEvent(e);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"An error occurred processing event '{eventTypeName}' for aggregate id '{e.Id}'");
                throw;
            }
        }

        public void SendCommand<TCommand>(TCommand c)
        {
            if (_commandHandlers.ContainsKey(c.GetType()))
            {
                _commandHandlers[c.GetType()](c);
            }
            else
            {
                throw new Exception("No command handler registered for " + c.GetType().Name);
            }
        }

        public void SendCommandAsync<TCommand>(TCommand c)
        {
            if (SingleThreadedMode)
            {
                SendCommand(c);
                return;
            }

            if (_commandHandlers.ContainsKey(c.GetType()))
            {
                _commandQueue.Enqueue((ICommand) c);
            }
            else
            {
                throw new Exception("No command handler registered for " + typeof(TCommand).Name);
            }
        }

        public void SendEvent(IEvent e)
        {
            var aggregateEventCount = _eventStore.GetAggregateEventCount(e.Id);

            if (aggregateEventCount == 0)
            {
                _eventStore.SaveAggregate(e.Id, typeof(T));
            }

            _logger.Debug($"Event type '{e.GetType().Name}' saving for aggregate id '{e.Id}'");
            _eventStore.SaveEvents(e.Id, new List<IEvent> {e}, typeof(T));

            _logger.Debug($"Event type '{e.GetType().Name}' publishing for aggregate id '{e.Id}'");
            PublishEvent(e);

            _logger.Debug($"Event type '{e.GetType().Name}' routing for aggregate id '{e.Id}'");
            RouteEvent(e);
        }

        public void SendEventAsync(IEvent e, Type aggregateType)
        {
            var aggregateEventCount = _eventStore.GetAggregateEventCount(e.Id);
            if (aggregateEventCount == 0) _eventStore.SaveAggregate(e.Id, aggregateType);

            _eventQueue.Enqueue(e);
        }

        private void PublishEvent(object e)
        {
            var eventType = e.GetType();

            if (!_eventSubscribers.ContainsKey(eventType))
            {
                return;
            }

            foreach (var sub in _eventSubscribers[eventType])
            {
                _logger.Debug(
                    $"Event type '{e.GetType().Name}' publishing to subscriber for aggregate id '{((IEvent)e).Id}'");
                sub(e);
            }
        }

        private void RouteEvent(object e)
        {
            var eventType = e.GetType();

            if (!_routers.ContainsKey(eventType))
            {
                return;
            }

            foreach (var router in _routers[eventType])
            {
                router(e);
            }
        }

        public void AddRouterFor<TEvent>(IRoute<TEvent> router)
        {
            if (!_routers.ContainsKey(typeof(TEvent)))
            {
                _routers.Add(typeof(TEvent), new List<Action<object>>());
            }

            _routers[typeof(TEvent)].Add(e => router.Handle((TEvent)e));
            _logger.Debug($"Registered router '{router.GetType().Name}' for event type '{typeof(TEvent).Name}'");
        }

        public void AddSubscriberFor<TEvent>(ISubscribeTo<TEvent> subscriber)
        {
            if (!_eventSubscribers.ContainsKey(typeof(TEvent)))
            {
                _eventSubscribers.Add(typeof(TEvent), new List<Action<object>>());
            }

            _eventSubscribers[typeof(TEvent)].Add(e => subscriber.Handle((TEvent)e));
            _logger.Debug(
                $"Registered event subscriber '{subscriber.GetType().Name}' for event type '{typeof(TEvent).Name}'");
        }

        public void AddHandlerFor<TCommand>(IHandleCommand<TCommand> commandHandler)
        {
            if (!_commandHandlers.ContainsKey(typeof(TCommand)))
            {
                _commandHandlers.Add(typeof(TCommand), c =>
                {
                    _logger.Debug($"Command type '{c.GetType().Name}' received for aggregate id '{((ICommand)c).Id}'");

                    var events = new List<IEvent>();
                    foreach (var e in commandHandler.Handle((TCommand)c))
                        events.Add((IEvent)e);

                    _logger.Debug($"Command type '{c.GetType().Name}' handled by command handler '{commandHandler.GetType().Name}'");

                    if (events.Count > 0)
                        _eventStore.SaveEvents(events.First().Id, events, typeof(T));

                    foreach (var e in events)
                    {
                        _logger.Debug($"Command handler for type '{c.GetType().Name}' emitted event type '{e.GetType().Name}' for aggregate id '{((ICommand)c).Id}'");
                        PublishEvent(e);
                    }

                    foreach (var e in events)
                    {
                        _logger.Debug($"Event type '{e.GetType().Name}' routing for aggregate id '{((ICommand)c).Id}'");
                        RouteEvent(e);
                    }
                });
            }

            _logger.Debug($"Registered command handler '{commandHandler.GetType().Name}' for command type '{typeof(TCommand).Name}'");
        }

        public void ScanInstance(object instance)
        {
            var handlers = GetImplementationsOfInterface(instance, typeof(IHandleCommand<>));
            var subscribers = GetImplementationsOfInterface(instance, typeof(ISubscribeTo<>));
            var routers = GetImplementationsOfInterface(instance, typeof(IRoute<>));

            var addHandlerForMethodInfo = GetType().GetMethod(nameof(AddHandlerFor));
            var addSubscriberForMethodInfo = GetType().GetMethod(nameof(AddSubscriberFor));
            var addRouterForMethodInfo = GetType().GetMethod(nameof(AddRouterFor));

            var parameters = new[] {instance};

            handlers.ForEach(h => addHandlerForMethodInfo.MakeGenericMethod(h).Invoke(this, parameters));
            subscribers.ForEach(s => addSubscriberForMethodInfo.MakeGenericMethod(s).Invoke(this, parameters));
            routers.ForEach(r => addRouterForMethodInfo.MakeGenericMethod(r).Invoke(this, parameters));
        }

        private static IEnumerable<Type> GetImplementationsOfInterface(object instance, Type interfaceType)
        {
            return instance.GetType().GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType)
                .Select(i => i.GetGenericArguments()[0]).ToList();
        }

        public void AssertRegistration(Assembly asm, bool throwExceptionOnFailure)
        {
            var types = asm.GetTypes();
            var errors = new StringBuilder();

            foreach (var t in types)
            {
                AssertHandlersRegistration(t, errors);

                AssertSubscribersRegistration(t, errors);

                AssertRoutersRegistration(t, errors);
            }

            if (errors.Length <= 0)
            {
                return;
            }

            var message = errors.ToString();

            if (throwExceptionOnFailure)
            {
                throw new RegistrationFailureException(message);
            }

            _logger.Warning(message);
        }

        private void AssertRoutersRegistration(Type t, StringBuilder errors)
        {
            var routers = t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRoute<>))
                .Select(i => new {Router = t, EventType = i.GetGenericArguments()[0]}).ToList();

            if (routers.Count > 0)
                routers.ForEach(r =>
                {
                    var exists = _routers.ContainsKey(r.EventType);
                    if (!exists)
                        errors.AppendLine($"EventRouter '{r.Router.Name}' not registered for event '{r.EventType.Name}'");
                });
        }

        private void AssertSubscribersRegistration(Type t, StringBuilder errors)
        {
            var subscribers = t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISubscribeTo<>))
                .Select(i => new {Subscriber = t, EventType = i.GetGenericArguments()[0]}).ToList();

            if (subscribers.Count > 0)
                subscribers.ForEach(s =>
                {
                    var exists = _eventSubscribers.ContainsKey(s.EventType);
                    if (!exists)
                        errors.AppendLine(
                            $"EventSubscriber '{s.Subscriber.Name}' not registered for event '{s.EventType.Name}'");
                });
        }

        private void AssertHandlersRegistration(Type t, StringBuilder errors)
        {
            var handlers = t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHandleCommand<>))
                .Select(y => new {Handler = t, CommandType = y.GetGenericArguments()[0]}).ToList();

            if (handlers.Count > 0)
                handlers.ForEach(h =>
                {
                    var exists = _commandHandlers.ContainsKey(h.CommandType);
                    if (!exists)
                        errors.AppendLine(
                            $"CommandHandler '{h.Handler.Name}' not registered for command '{h.CommandType.Name}'");
                });
        }
    }
}
