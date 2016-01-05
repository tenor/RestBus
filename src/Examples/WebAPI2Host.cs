using Microsoft.Owin.Hosting;
using Owin;
using RestBus.RabbitMQ;
using RestBus.RabbitMQ.Subscription;
using RestBus.WebApi;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Http;

namespace Examples
{
    public class WebAPI2Host
    {
        public IList<IDisposable> Start()
        {
            List<IDisposable> servers = new List<IDisposable>();

            //Initialize startup object
            var startup = new Startup();

            string baseAddress = null;
            //baseAddress = "http://localhost:9000/"; //Uncomment this line to also listen via HTTP on localhost:9000

            //Start WebAPI OWIN host 
            servers.Add(WebApp.Start(url: baseAddress, startup: startup.Configuration));

            //Start RestBus Subscriber/host

            var amqpUrl = ConfigurationManager.AppSettings["rabbitmqserver"]; //AMQP URI for RabbitMQ server
            var serviceName = "test"; //Uniquely identifies this service

            var msgMapper = new BasicMessageMapper(amqpUrl, serviceName);
            var subscriber = new RestBusSubscriber(msgMapper);
            var host = new RestBusHost(subscriber, startup.Config);

            host.Start();
            Console.WriteLine("Server started ... Ctrl-C to quit.");

            servers.Add(host);
            return servers;

        }
    }

    public class Startup
    {
        HttpConfiguration config = new HttpConfiguration();

        public HttpConfiguration Config
        {
            get { return config; }
        }

        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            appBuilder.UseWebApi(config);

        }
    }

    public class TestController : ApiController
    {
        // POST api/test 
        [HttpPost]
        public IHttpActionResult Post([FromBody]Value value)
        {
            return Ok( new { Message = "Request Was Received", 
                Time = DateTime.Now, 
                Value = value.Val });
        }

        public class Value
        {
            public int? Val { get; set; }
        }
    }
}
