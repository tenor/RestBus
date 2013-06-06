using RestBus.RabbitMQ.Client;
using RestBus.RabbitMQ;
//using RestBus.RabbitMQ.Subscriber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Examples
{
    class Program
    {
        static void Main(string[] args)
        {
            BasicExchangeMapper exchangeMapper = new BasicExchangeMapper("amqp:localhost:5672", "test");
            RestBusClient client = new RestBusClient(exchangeMapper);

            var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://localhost/test/restbus/")
            {
                Content = new System.Net.Http.StringContent("{\"Val\":10}", new UTF8Encoding(), "application/json")
            };

            msg.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01, */*; q=0.01");

            var m = client.SendAsync(msg, System.Threading.CancellationToken.None).Result;

            //TODO: client.Dispose should kill that background thread that's actually in the foreground and doesn't let this sample die.

            //RabbitMQRestBusSubscriber subscriber = new RabbitMQRestBusSubscriber(exchangeMapper);
            //subscriber.Start();
        }
    }
}
