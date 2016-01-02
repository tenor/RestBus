using System.Collections.Generic;
using System.Net.Http;
namespace RestBus.Common.Amqp
{
    public interface IMessageMapper
    {
        ExchangeInfo GetExchangeInfo();
        string GetRoutingKey(HttpRequestMessage request);
        IDictionary<string, object> GetHeaders(HttpRequestMessage request);
    }
}
