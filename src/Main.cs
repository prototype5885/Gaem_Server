using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace Gaem_server;

public class MainClass
{
    public static async Task Main()
    {
        //ConfigureLog4Net();
        Server server = new Server();
        await server.StartServer();
    }
    //private static void ConfigureLog4Net()
    //{
    //    // Get the root logger
    //    var repository = (Hierarchy)LogManager.GetRepository();
    //    var rootLogger = repository.Root;

    //    // Clear existing appenders
    //    rootLogger.RemoveAllAppenders();

    //    // Add new appenders programmatically
    //    var consoleAppender = new ConsoleAppender
    //    {
    //        Layout = new PatternLayout("%d{HH:mm:ss} [%thread] %-5level %logger - %message%newline")
    //    };
    //    rootLogger.AddAppender(consoleAppender);

    //    // Set log level
    //    rootLogger.Level = log4net.Core.Level.Debug;

    //    // Apply changes to the configuration
    //    repository.Configured = true;
    //}
}