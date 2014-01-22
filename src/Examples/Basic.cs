using RestBus.RabbitMQ;
using RestBus.RabbitMQ.Client;
using System;
using System.Text;

namespace Examples
{
    static class Basic
    {
        public static void Run()
        {
            /*
             * An example that composes a HttpRequest Message and sends it via the RestBus RabbitMQ client.
             * 
             * For more examples, see the https://github.com/tenor/RestBus.Examples repo
             * 
             */

            BasicMessageMapper msgMapper = new BasicMessageMapper("amqp://localhost:5672", "test");
            RestBusClient client = new RestBusClient(msgMapper);

            var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "/test/my_api")
            {
                Content = new System.Net.Http.StringContent("{\"Val\":10}", new UTF8Encoding(), "application/json")
            };

            msg.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01, */*; q=0.01");

            var res = client.SendAsync(msg, System.Threading.CancellationToken.None).Result;


            Console.ReadKey();
            client.Dispose();
        }
    }
}
