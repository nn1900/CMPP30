using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using Reefoo.CMPP30;

namespace cmpp30test
{
  class Program
  {
    private static readonly HashSet<long> _msgIds = new HashSet<long>();
    private static int _totalSent;
    //private static Cmpp30Configuration _config = new Cmpp30Configuration
    //{
    //  GatewayIp = "211.137.85.115",
    //  GatewayPort = 7891,
    //  GatewayUsername = "480934",
    //  GatewayPassword = "TW480934",
    //  SpCode = "106573210388",
    //  ServiceId = "MSC0010501",
    //  GatewaySignature = "【兴业云】",
    //  PrepositiveGatewaySignature = true,
    //  AttemptRemoveSignature = false,
    //  DisableLongMessage = false,
    //  SendLongMessageAsShortMessages = false,
    //};

    private static readonly Cmpp30Configuration _config; 

    static Program()
    {
      var settings = ConfigurationManager.AppSettings;
      _config = new Cmpp30Configuration
      {
        GatewayIp = settings["cmpp3.gateway.ip"], // 网关IP
        GatewayPort = int.Parse(settings["cmpp3.gateway.port"]), // 网关端口
        GatewayUsername = settings["cmpp3.gateway.username"], // 网关登录用户名
        GatewayPassword = settings["cmpp3.gateway.password"], // 网关登录密码
        SpId = settings["cmpp3.service.spid"], // SP的企业代码
        SpCode = settings["cmpp3.service.spcode"], // SP服务代码，将显示在用户手机上的短信主叫号码
        ServiceId = settings["cmpp3.service.service_id"], // 业务代码（业务标识）
        GatewaySignature = "【" + settings["cmpp3.service.signature"] + "】", // 端口签名
        PrepositiveGatewaySignature = bool.Parse(settings["cmpp3.prepositive_gateway_signature"]),
        AttemptRemoveSignature = bool.Parse(settings["cmpp3.attemp_remove_signature"]),
        DisableLongMessage = bool.Parse(settings["cmpp3.disable_long_message"]),
        SendLongMessageAsShortMessages = bool.Parse(settings["cmpp3.send_long_message_as_short_messages"]),
      };
    }


    public static void AddConsoleLogger(bool colored = true)
    {
      var repositories = log4net.LogManager.GetAllRepositories();
      IAppender appender;
      if (colored)
      {
        var consoleAppender = new ColoredConsoleAppender();
        //consoleAppender.Target = "Console.Out"; 
        consoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
          Level = Level.Debug,
          ForeColor = ColoredConsoleAppender.Colors.Cyan
        });
        consoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
          Level = Level.Info,
          ForeColor = ColoredConsoleAppender.Colors.Purple | ColoredConsoleAppender.Colors.HighIntensity
        });
        consoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
          Level = Level.Warn,
          ForeColor = ColoredConsoleAppender.Colors.Yellow | ColoredConsoleAppender.Colors.HighIntensity
        });
        consoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
          Level = Level.Error,
          ForeColor = ColoredConsoleAppender.Colors.Red | ColoredConsoleAppender.Colors.HighIntensity
        });
        consoleAppender.AddMapping(new ColoredConsoleAppender.LevelColors
        {
          Level = Level.Fatal,
          ForeColor = ColoredConsoleAppender.Colors.Red | ColoredConsoleAppender.Colors.HighIntensity,
          BackColor = ColoredConsoleAppender.Colors.White | ColoredConsoleAppender.Colors.HighIntensity
        });
        var patternLayout = new PatternLayout("[%d{HH:mm:ss}][%thread] %logger - %message%newline")
        {
          IgnoresException = false
        };
        consoleAppender.Layout = patternLayout;

        // add bufferred appender decorator
        appender = consoleAppender;
        consoleAppender.ActivateOptions();
      }
      else
      {
        var consoleAppender = new ConsoleAppender
        {
          Layout = new PatternLayout("[%d{HH:mm:ss}][%thread] %-5level %logger - %message%newline")
          {
            IgnoresException = false
          }
        };
        appender = consoleAppender;
      }

      var bufferredForwardingAppender = new BufferingForwardingAppender();
      bufferredForwardingAppender.AddAppender(appender);
      bufferredForwardingAppender.BufferSize = 512;
      bufferredForwardingAppender.Fix = FixFlags.Exception;

      appender = bufferredForwardingAppender;

      BasicConfigurator.Configure(appender);

      foreach (var repo in repositories)
      {
        var hierarchy = repo as log4net.Repository.Hierarchy.Hierarchy;
        if (null != hierarchy)
        {
          hierarchy.Root.AddAppender(appender);
        }
      }
    }

    private static ILog log = LogManager.GetLogger(typeof(Program));

    static void Main(string[] args)
    {
      Environment.CurrentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
      log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo("log4net.config"));

      AddConsoleLogger();

      var numbers = (ConfigurationManager.AppSettings["test.numbers"] ?? "")
        .Split(',').Select(x => x.Trim()).ToArray();

      using (var client = new Cmpp30Client(_config))
      {
        client.OnMessageReport += client_OnMessageReport;
        client.Start();

        log.Info("Connecting... ");
        for (var i = 0; i < 30; i++)
        {
          Thread.Sleep(1000);
          if (client.Status == Cmpp30ClientStatus.Connected) break;
        }

        if (client.Status != Cmpp30ClientStatus.Connected)
        {
          log.ErrorFormat("Client connection failed. Status: {0} {1}.", client.Status, client.StatusText);
          return;
        }

        log.Info("Connected.");

        List<long> msgIds;
        var status = client.Send(
          "", numbers, "测试发送短信", out msgIds
         );

        log.InfoFormat("Send status: {0}", status);
        while (true)
        {
          Thread.Sleep(2500);
          log.DebugFormat("Client status: {0}", client.Status);
        }
      }
    }

    static void client_OnMessageReport(object sender, ReportEventArgs e)
    {
      log.InfoFormat(
        "status report: msgid={0}, dest={1}, status={2}",
        e.MessageId,
        e.Destination,
        e.StatusText
      );
    }
  }
}
