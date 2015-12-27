using System.Collections.Generic;
using System.Net.Http;
namespace RestBus.Common.Amqp
{
    public interface IMessageMapper
    {
        ExchangeInfo GetExchangeInfo();
        bool PersistentMessages { get; }
        bool PersistentWorkQueuesAndExchanges { get; }
        string GetRoutingKey(HttpRequestMessage request);
        IDictionary<string, object> GetHeaders(HttpRequestMessage request);
        bool GetExpires(HttpRequestMessage request);
    }
}
