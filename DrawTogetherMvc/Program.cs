using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Security.Authentication;

namespace DrawTogetherMvc
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(kestrelOptions =>
                    {
                        if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major == 7)
                        {
                            kestrelOptions.ConfigureHttpsDefaults(httpsOptions =>
                                httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13);
                        }
                    });
                    webBuilder.UseStartup<Startup>();
                });
    }
}
