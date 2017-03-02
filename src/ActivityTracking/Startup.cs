using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ActivityTracking
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime appLifetime)
        {
            loggerFactory.AddConsole();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
			var instrumentation = AspNetDiagListener.Enable();
			appLifetime?.ApplicationStopped.Register(() => instrumentation?.Dispose());

			app.Run(async (context) =>
            {
                await context.Response.WriteAsync("Hello World!");
            });

			var logger = loggerFactory.CreateLogger("Listener");
			DiagnosticListener.AllListeners.Subscribe(delegate (DiagnosticListener listener) {
				if (listener.Name == "Microsoft.AspNetCore")
				{
					listener.Subscribe(delegate (KeyValuePair<string, object> value)
					{
						if (Activity.Current != null)
							logger.LogInformation($"Event: {value.Key}, {Activity.Current.OperationName}, {Activity.Current.Id} ");
					});
				}
			});
        }
    }
}
