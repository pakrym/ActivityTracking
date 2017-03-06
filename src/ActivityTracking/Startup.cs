using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Microsoft.Extensions.DiagnosticAdapter;
using System.Net.Http;

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
            //simulate ASP.NET Core Diagnostics
            var instrumentation = AspNetDiagListener.Enable();

            //enable Dependency tracking in AI
            IDisposable dependencyInstr = Microsoft.ApplicationInsights.DependencyCollector.DependencyCollectorDiagnosticListener.Enable();
            appLifetime?.ApplicationStopped.Register(() => {instrumentation?.Dispose(); dependencyInstr?.Dispose(); });

            HttpClient hc = new HttpClient();
			app.Run(async (context) =>
            {
                await hc.GetAsync("http://localhost:3000");
                await context.Response.WriteAsync("Hello World!");
            });
        }
    }
}
