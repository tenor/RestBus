using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestBus.RabbitMQ;
using RestBus.RabbitMQ.Subscription;
using RestBus.AspNet.Server;
using RestBus.AspNet;
using System.Reflection;

namespace RestBusCoreTest
{

    public class Startup
    {

        public IConfigurationRoot Configuration { get; }

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsEnvironment("Development"))
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }


        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            //services.AddApplicationInsightsTelemetry(Configuration);

            services.AddMvc();

            services.AddRestBusServer(this.Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            //app.UseApplicationInsightsRequestTelemetry();

            //app.UseApplicationInsightsExceptionTelemetry();

            app.UseMvc();

            // Create RestBus Subscriber
            var amqpUrl = "amqp://192.168.0.80:5672"; //AMQP URI for RabbitMQ server
            var serviceName = "samba"; //Uniquely identifies this service

            var msgMapper = new BasicMessageMapper(amqpUrl, serviceName);
            var subscriber = new RestBusSubscriber(msgMapper);

            bool standAlone = false;
            /* 
                This service listens for requests through both HTTP and the message broker. 
                If you desire a standalone service that only listens to the message broker:
                1. Set standAlone to true
                2. Update the hosting.json file with the instructions in the file.
            */

            if (standAlone)
            {
                // Configures the rest bus server -- needed if running standalone server, ignored otherwise.
                app.ConfigureRestBusServer(subscriber);
            }
            else
            {
                app.RunRestBusHost(subscriber);
            }
        }

    }

}