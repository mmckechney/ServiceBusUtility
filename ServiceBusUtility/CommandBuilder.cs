using Spectre.Console;
using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceBusUtility
{
   internal class CommandBuilder
   {
      public static RootCommand BuildCommandLine()
      {
         RootCommand rootCommand = new RootCommand(
             $"Utility to help you send and receive test messages to a Service Bus Queue or Topic/Subscription. " +
             $"{Environment.NewLine}https://github.com/mmckechney/ServiceBusUtility");

         rootCommand.Add(BuildConnectionCommand());
         rootCommand.Add(BuildQueueFamilyCommand("queue", "Interact with a Service Bus Queue", QueueType.Queue, isTopicBased: false));
         rootCommand.Add(BuildQueueFamilyCommand("topic", "Interact with a Service Bus Topic/Subscription", QueueType.TopicSubscription, isTopicBased: true));
         rootCommand.Add(BuildQueueFamilyCommand("queuedl", "Interact with a Service Bus Queue deadletter sub-queue", QueueType.DeadLetterQueue, isTopicBased: false));
         rootCommand.Add(BuildQueueFamilyCommand("topicdl", "Interact with a Service Bus Topic/Subscription deadletter sub-queue", QueueType.DeadLetterSubscription, isTopicBased: true));

         // Customize help to show FigletText banner
         var helpOption = rootCommand.Options.OfType<HelpOption>().FirstOrDefault();
         if (helpOption != null)
         {
            helpOption.Action = new FigletHelpAction();
         }

         return rootCommand;
      }

      private static Command BuildQueueFamilyCommand(string name, string description, QueueType queueType, bool isTopicBased)
      {
         var command = new Command(name, description);
         command.Add(BuildSendCommand(queueType, isTopicBased));
         command.Add(BuildReadCommand(queueType, isTopicBased));
         command.Add(BuildPeekCommand(queueType));
         return command;
      }

      private static Command BuildSendCommand(QueueType queueType, bool isTopicBased)
      {
         var nsOption = new Option<string>("--ns", ["--sb-namespace"]) { Description = "Service Bus namespace to use for AAD authentication" };
         var countOption = new Option<int>("--count", ["-c"]) { Description = "Count of messages to add to the Service Bus Queue", Required = true };
         var waitOption = new Option<int>("--wait", ["-w"]) { Description = "Wait interval (in milliseconds) between message sends", DefaultValueFactory = _ => 0 };
         var delayOption = new Option<int>("--delay", ["-d"]) { Description = "Delay (schedule or reschedule) the message for X seconds in the future", DefaultValueFactory = _ => 0 };
         var quietOption = new Option<bool>("--quiet") { Description = "Don't output each individual \"Sent Message\" line", DefaultValueFactory = _ => false };

         string cmdDescription = isTopicBased ? "Send messages to a Service Bus topic/subscription" : "Send messages to a Service Bus queue";
         var cmd = new Command("send", cmdDescription);

         Option<string> queueOption = null;
         Option<string> topicOption = null;
         Option<string> subOption = null;

         if (isTopicBased)
         {
            topicOption = new Option<string>("--topic-name", ["-t"]) { Description = "Topic to Read or Send messages" };
            subOption = new Option<string>("--subscription-name", ["--sub"]) { Description = "Topic to Read Send messages" };
            cmd.Add(topicOption);
            cmd.Add(subOption);
         }
         else
         {
            queueOption = new Option<string>("--queue-name", ["-q"]) { Description = "Queue to Read or Send messages" };
            cmd.Add(queueOption);
         }

         cmd.Add(nsOption);
         cmd.Add(countOption);
         cmd.Add(waitOption);
         cmd.Add(delayOption);
         cmd.Add(quietOption);

         cmd.SetAction(async (parseResult) =>
         {
            var sbSettings = BuildServiceBusSettings(parseResult, nsOption, queueOption, topicOption, subOption);
            await Worker.SendMessages(
                parseResult.GetValue(countOption),
                parseResult.GetValue(waitOption),
                parseResult.GetValue(delayOption),
                queueType,
                sbSettings,
                parseResult.GetValue(quietOption));
         });

         return cmd;
      }

      private static Command BuildReadCommand(QueueType queueType, bool isTopicBased)
      {
         var nsOption = new Option<string>("--ns", ["--sb-namespace"]) { Description = "Service Bus namespace to use for AAD authentication" };
         var messageHandlingOption = new Option<MessageHandling>("--messagehandling", ["--mh"]) { Description = "How to treat messages retrieved from Queue", DefaultValueFactory = _ => MessageHandling.Complete };
         var messageIdOption = new Option<long?>("--num", ["--sequence-number"]) { Description = "Sequence Number for specific message to read" };
         var delayOption = new Option<int>("--delay", ["-d"]) { Description = "Delay (schedule or reschedule) the message for X seconds in the future", DefaultValueFactory = _ => 0 };
         var isScheduledOption = new Option<bool>("--scheduled", ["-s"]) { Description = "Whether or not to look for scheduled messages (valid only for 'topic' read or peek)" };

         string cmdDescription = isTopicBased
             ? "Read messages from a Topic/Subscription, with various ways to handle a message"
             : "Read messages from a Queue, with various ways to handle a message";
         var cmd = new Command("read", cmdDescription);

         Option<string> queueOption = null;
         Option<string> topicOption = null;
         Option<string> subOption = null;

         if (isTopicBased)
         {
            topicOption = new Option<string>("--topic-name", ["-t"]) { Description = "Topic to Read or Send messages" };
            subOption = new Option<string>("--subscription-name", ["--sub"]) { Description = "Topic to Read Send messages" };
            cmd.Add(topicOption);
            cmd.Add(subOption);
         }
         else
         {
            queueOption = new Option<string>("--queue-name", ["-q"]) { Description = "Queue to Read or Send messages" };
            cmd.Add(queueOption);
         }

         cmd.Add(nsOption);
         cmd.Add(messageHandlingOption);
         cmd.Add(messageIdOption);
         cmd.Add(delayOption);
         cmd.Add(isScheduledOption);

         cmd.SetAction(async (parseResult) =>
         {
            var sbSettings = BuildServiceBusSettings(parseResult, nsOption, queueOption, topicOption, subOption, messageHandlingOption);
            await Worker.ReadMessage(
                parseResult.GetValue(messageIdOption),
                queueType,
                parseResult.GetValue(delayOption),
                parseResult.GetValue(isScheduledOption),
                sbSettings);
         });

         return cmd;
      }

      private static Command BuildPeekCommand(QueueType queueType)
      {
         var countOption = new Option<int>("--count", ["-c"]) { Description = "Count of messages to peek from the Service Bus Queue", DefaultValueFactory = _ => 50 };
         var isScheduledOption = new Option<bool>("--scheduled", ["-s"]) { Description = "Whether or not to look for scheduled messages (valid only for 'topic' read or peek)" };

         var cmd = new Command("peek", "Peek messages from a Service Bus");
         cmd.Add(countOption);
         cmd.Add(isScheduledOption);

         cmd.SetAction(async (parseResult) =>
         {
            await Worker.PeekMessages(
                parseResult.GetValue(countOption),
                queueType,
                parseResult.GetValue(isScheduledOption),
                new ServiceBusSettings(null));
         });

         return cmd;
      }

      private static Command BuildConnectionCommand()
      {
         var connectionCommand = new Command("connection", "Sets or clears a Service Bus Connection string. If no connection string is specified, Azure RBAC will be used.");

         var connStringOption = new Option<string>("--connection-string", ["-c"]) { Description = "Connection String to your Service Bus", Required = true };
         var exitOption = new Option<bool>("--exit", ["-e"]) { Description = "Forces close of app after method runs", DefaultValueFactory = _ => false, Hidden = true };

         var setConnectionCommand = new Command("set", "Sets the connection string for your target service bus");
         setConnectionCommand.Add(connStringOption);
         setConnectionCommand.Add(exitOption);
         setConnectionCommand.SetAction((parseResult) =>
         {
            Worker.SetConnectionString(
                parseResult.GetValue(connStringOption),
                parseResult.GetValue(exitOption));
         });

         var clearConnectionCommand = new Command("clear", "Clears a saved connection string");
         clearConnectionCommand.SetAction((parseResult) =>
         {
            Worker.ClearConnectionString();
         });

         connectionCommand.Add(setConnectionCommand);
         connectionCommand.Add(clearConnectionCommand);

         return connectionCommand;
      }

      private static ServiceBusSettings BuildServiceBusSettings(ParseResult parseResult, Option<string> nsOption,
          Option<string> queueOption = null, Option<string> topicOption = null, Option<string> subOption = null,
          Option<MessageHandling> messageHandlingOption = null)
      {
         var settings = new ServiceBusSettings(null);
         settings.SbNamespace = parseResult.GetValue(nsOption) ?? "";
         if (queueOption != null) settings.QueueName = parseResult.GetValue(queueOption) ?? "";
         if (topicOption != null) settings.TopicName = parseResult.GetValue(topicOption) ?? "";
         if (subOption != null) settings.SubscriptionName = parseResult.GetValue(subOption) ?? "";
         if (messageHandlingOption != null) settings.MessageHandling = parseResult.GetValue(messageHandlingOption);
         return settings;
      }
   }

   internal class FigletHelpAction : SynchronousCommandLineAction
   {
      public override int Invoke(ParseResult parseResult)
      {
         AnsiConsole.Write(new FigletText("Service Bus Utility"));
         var defaultHelp = new HelpAction();
         return defaultHelp.Invoke(parseResult);
      }
   }
}
