using RestBus.RabbitMQ.Client;
using RestBus.RabbitMQ;
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
            /*
             * An example that composes a HttpRequest Message and sends it via the RestBus RabbitMQ client.
             * 
             * For more examples, see the https://github.com/tenor/RestBus.Examples repo
             * 
             */

            BasicMessageMapper msgMapper = new BasicMessageMapper("amqp:localhost:5672", "test");
            RestBusClient client = new RestBusClient(msgMapper);

            var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "http://localhost/test/restbus/")
            {
                Content = new System.Net.Http.StringContent("{\"Val\":10}", new UTF8Encoding(), "application/json")
            };

            msg.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01, */*; q=0.01");

            //TODO: This doesn't return a timeout error when the message times out it returns ("Error in the application")
            var m = client.SendAsync(msg, System.Threading.CancellationToken.None).Result;

            client.Dispose();

        }
    }
}
