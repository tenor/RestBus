using RestBus.RabbitMQ.Common;
using System.Net.Http;
namespace RestBus.RabbitMQ
{
    public interface IExchangeMapper
    {
        ExchangeInfo GetExchangeInfo();
        string GetRoutingKey(HttpRequestMessage request);
        bool GetExpires(HttpRequestMessage request);
    }
}
