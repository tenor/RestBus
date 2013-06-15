using System.Net.Http;
namespace RestBus.RabbitMQ.Common
{
    public interface IExchangeMapper
    {
        ExchangeInfo GetExchangeInfo();
        string GetRoutingKey(HttpRequestMessage request);
        bool GetExpires(HttpRequestMessage request);
    }
}
