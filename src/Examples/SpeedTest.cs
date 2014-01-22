using RestBus.RabbitMQ;
using RestBus.RabbitMQ.Client;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace Examples
{
    public class SpeedTest
    {
        public static void Run(int iterations)
        {
            /*
             * An example that performs a speed test via the RestBus RabbitMQ client.
             * 
             * For more examples, see the https://github.com/tenor/RestBus.Examples repo
             * 
             */

            BasicMessageMapper msgMapper = new BasicMessageMapper("amqp://localhost:5672", "test");
            RestBusClient client = new RestBusClient(msgMapper);

            var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "api/hello/random")
            {
                Content = new System.Net.Http.StringContent("{\"Val\":10}", new UTF8Encoding(), "application/json")
            };

            msg.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01, */*; q=0.01");

            Stopwatch watch = new Stopwatch();

            HttpResponseMessage res;
            watch.Start();
            for (int i = 0; i < iterations; i++)
            {
                res = client.SendAsync(msg, System.Threading.CancellationToken.None).Result;
            }

            watch.Stop();

            Console.WriteLine("Elapsed time: " + watch.Elapsed);
            client.Dispose();
            Console.ReadKey();
        }
    }
}
