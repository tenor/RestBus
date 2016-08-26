using RestBus.Client;
using RestBus.RabbitMQ;
using RestBus.RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RestBusCoreTest.Client
{

    public class Program
    {

        public static void Main(string[] args)
        {
            var amqpUrl = "amqp://192.168.0.80:5672"; //AMQP URL for RabbitMQ installation
            var serviceName = "samba"; //The unique identifier for the target service

            var msgMapper = new BasicMessageMapper(amqpUrl, serviceName);

            RestBusClient client = new RestBusClient(msgMapper);

            RequestOptions requestOptions = null;
            /* 
             * //Uncomment this section to get a response in JSON format
             *  */
            requestOptions = new RequestOptions();
            requestOptions.Headers.Add("Accept", "application/json");


            //Send Request
            var uri = "api/values"; //Substitute "hello/random" for the ServiceStack example
            var response = client.GetAsync(uri, requestOptions).Result;
            //NOTE: You can use 'var response = await client.GetAsync(uri, requestOptions);' in your code

            //Display response
            Console.WriteLine(response.StatusCode);
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);

            //Dispose client
            client.Dispose();

            Console.ReadKey();
        }

    }

}