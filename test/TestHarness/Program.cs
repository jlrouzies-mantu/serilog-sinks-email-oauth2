using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Debugging;
using System.Reflection;

SelfLog.Enable(Console.Error);
var exeLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

var configuration = new ConfigurationBuilder()
                .SetBasePath(exeLocation!)
                .AddJsonFile("appsettings.json")
                //.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
                .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

Log.Information("Hello, world!");

await Log.CloseAndFlushAsync();
