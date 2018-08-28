using System;
using System.Reflection;

namespace Jodie
{
    public interface IMessageDispatcher
    {
        bool SingleThreadedMode { get; set; }
        void AddHandlerFor<TCommand>(IHandleCommand<TCommand> commandHandler);
        void AddRouterFor<TEvent>(IRoute<TEvent> router);
        void AddSubscriberFor<TEvent>(ISubscribeTo<TEvent> subscriber);
        void AssertRegistration(Assembly asm, bool throwExceptionOnFailure);
        void ScanInstance(object instance);
        void SendCommand<TCommand>(TCommand c);
        void SendCommandAsync<TCommand>(TCommand c);
        void SendEvent(IEvent e);
        void SendEventAsync(IEvent e, Type aggregateType);
        void Start();
        void Stop();
    }
}