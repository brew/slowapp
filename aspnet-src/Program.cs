using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace slowapp
{
    public class Program
    {
        public static void Main(string[] args)
        {

            // From https://github.com/aspnet/KestrelHttpServer/issues/2104
            System.Net.ServicePointManager.DefaultConnectionLimit = 256;
            System.Threading.ThreadPool.GetMaxThreads(out int _, out int completionThreads);
            System.Threading.ThreadPool.SetMinThreads(1024, completionThreads);
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
