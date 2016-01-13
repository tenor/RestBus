using System;
using System.Collections.Generic;

namespace RestBus.Common
{
    public interface IRestBusSubscriber : IDisposable
    {
        string Id { get; }

        IList<string> ConnectionNames { get; }

        MessageContext Dequeue();

        void Start();

        void SendResponse(MessageContext context, HttpResponsePacket response);

        //TODO: Add ConnectionStarted/ConnectionSucceeded/ConnectionFailed/ConnectionLost/Disconnected/ReconnectionStarted/ReconnectionFailure events.
        //NOTE: Disconnected occurs when application requested the disconnect and not dropped by server or network.
        //Decide if the actual connection object will be exposed by interface or just connectionName (which is safer).
        //Consider omittting the Started events so only Succeeded/Failed/Lost events are exposed
        //Consider removing Reconnected events and expose them via ConnectionStarted/Failed events
        //Consider passing the error message in ConnectionFailure to the event (also for Lost events if there is any exception)
        //Consider exposing a dictionary like event interface where individual events can be subscribed to/unsubscribed from 
        //so that new events can be added without modifying the interface. -- If so make the events discoverable.

    }
}
