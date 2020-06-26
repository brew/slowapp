using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace slowapp
{
    public class Startup
    {
        private void Log(string text) => Console.WriteLine($"{DateTime.UtcNow:dd/MM/yy HH:mm:ss}: {text}");

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime appLifetime)
        {
            Log($"Application starting - Host: {Environment.MachineName}, Process ID: {Process.GetCurrentProcess().Id}");

            appLifetime.ApplicationStarted.Register(ApplicationStarted);
            appLifetime.ApplicationStopping.Register(ApplicationStopping);
            appLifetime.ApplicationStopped.Register(ApplicationStopped);

            app.UseMiddleware<RequestLogger>();
            
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
        
        private void ApplicationStarted()
        {
            States.Running.SetState();
            Log("ApplicationStarted called");
        }
        
        private void ApplicationStopping()
        {
            States.AfterSigterm.SetState();
            Log("ApplicationStopping called");
        }

        private void ApplicationStopped()
        {
            Log("ApplicationStopped called");
        }
    }
}
