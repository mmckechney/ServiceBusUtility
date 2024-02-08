using Spectre.Console;
using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Linq;

namespace ServiceBusUtility
{
   internal class CommandBuilder
   {
      public static Parser BuildCommandLine()
      {
         var serviceBusNamespaceOption = new Option<string>(new string[] { "--ns", "--sb-namespace" }, "Service Bus namespace to use for AAD authentication");
         var queueNameOption = new Option<string>(new string[] { "-q", "--queue-name" }, "Queue to Read or Send messages");
         var topicNameOption = new Option<string>(new string[] { "-t", "--topic-name" }, "Topic to Read or Send messages");
         var subscriptionNameOption = new Option<string>(new string[] { "--sub", "--subscription-name" }, "Topic to Read Send messages");

         var messageCountOption = new Option<int>(new string[] { "--count", "-c" }, "Count of messages to add to the Service Bus Queue") { IsRequired = true };
         var waitIntervalOption = new Option<int>(new string[] { "--wait", "-w" }, () => 0, "Wait interval (in milliseconds) between message sends");
         var delayOption = new Option<int>(new string[] { "--delay", "-d" }, () => 0, "Delay (schedule or reschedule) the message for X seconds in the future");
         var quietOption = new Option<bool>(new string[] { "--quiet" }, () => false, "Don't output each individual \"Sent Message\" line");

         var queueSendCommand = new Command("send", "Send mesasages to a Service Bus queue");
         queueSendCommand.Handler = CommandHandler.Create<int, int, int, QueueType, ServiceBusSettings, bool>(Worker.SendMessages);
         queueSendCommand.Add(queueNameOption);
         queueSendCommand.Add(serviceBusNamespaceOption);
         queueSendCommand.Add(messageCountOption);
         queueSendCommand.Add(waitIntervalOption);
         queueSendCommand.Add(delayOption);
         queueSendCommand.Add(quietOption);

         var topicSendCommand = new Command("send", "Send mesasages to a Service Bus topic/subscription");
         topicSendCommand.Handler = CommandHandler.Create<int, int, int, QueueType, ServiceBusSettings, bool>(Worker.SendMessages);
         topicSendCommand.Add(topicNameOption);
         topicSendCommand.Add(subscriptionNameOption);
         topicSendCommand.Add(serviceBusNamespaceOption);
         topicSendCommand.Add(messageCountOption);
         topicSendCommand.Add(waitIntervalOption);
         topicSendCommand.Add(delayOption);
         topicSendCommand.Add(quietOption);


         var messageHandlingOption = new Option<MessageHandling>(new string[] { "--messagehandling", "--mh" }, () => MessageHandling.Complete, "How to treat messages retrieved from Queue");
         var messageIdOption = new Option<long?>(new string[] { "--num", "--sequence-number" }, "Sequence Number for specific message to read");
         var isScheduledOption = new Option<bool>(new string[] { "--scheduled", "-s" }, "Whether or not to look for scheduled messages (valid only for 'topic' read or peek)");
         var readQueueCommand = new Command("read", "Read messages from a Queue, with various ways to handle a message");
         readQueueCommand.Handler = CommandHandler.Create<long?, QueueType, int, bool, ServiceBusSettings>(Worker.ReadMessage);
         readQueueCommand.Add(queueNameOption);
         readQueueCommand.Add(serviceBusNamespaceOption);
         readQueueCommand.Add(messageHandlingOption);
         readQueueCommand.Add(messageIdOption);
         readQueueCommand.Add(delayOption);
         readQueueCommand.Add(isScheduledOption);

         var readTopicCommand = new Command("read", "Read messages from a Topic/ Subscriptiopn, with various ways to handle a message");
         readTopicCommand.Handler = CommandHandler.Create<long?, QueueType, int, bool, ServiceBusSettings>(Worker.ReadMessage);
         readTopicCommand.Add(topicNameOption);
         readTopicCommand.Add(subscriptionNameOption);
         readTopicCommand.Add(serviceBusNamespaceOption);
         readTopicCommand.Add(messageHandlingOption);
         readTopicCommand.Add(messageIdOption);
         readTopicCommand.Add(delayOption);
         readTopicCommand.Add(isScheduledOption);

         var peekCommand = new Command("peek", "Peek messages from a Service Bus");
         peekCommand.Handler = CommandHandler.Create<int, QueueType, bool, ServiceBusSettings>(Worker.PeekMessages);
         peekCommand.Add(new Option<int>(new string[] { "--count", "-c" }, () => 50, "Count of messages to peek from the Service Bus Queue"));
         peekCommand.Add(isScheduledOption);

         var queueCommand = new Command("queue", "Interact with a Service Bue Queue")
            {
                 new Option<QueueType>("queueType", () => QueueType.Queue) { IsHidden = true }
            };
         queueCommand.Add(queueSendCommand);
         queueCommand.Add(readQueueCommand);
         queueCommand.Add(peekCommand);

         var queueDeadLetterCommand = new Command("queuedl", "Interact with a Service Bue Queue deadletter sub-queue")
            {
                 new Option<QueueType>("queueType", () => QueueType.DeadLetterQueue) { IsHidden = true }
            };
         queueDeadLetterCommand.Add(queueSendCommand);
         queueDeadLetterCommand.Add(readQueueCommand);
         queueDeadLetterCommand.Add(peekCommand);

         var topicCommand = new Command("topic", "Interact with a Service Bus Topic/Subscription")
            {
                 new Option<QueueType>("queueType", () => QueueType.TopicSubscription) { IsHidden = true }
            };
         topicCommand.Add(topicSendCommand);
         topicCommand.Add(readTopicCommand);
         topicCommand.Add(peekCommand);

         var topicDeadletterCommand = new Command("topicdl", "Interact with a Service Bus Topic/Subscription deadletter sub-queue")
            {
                 new Option<QueueType>("queueType", () => QueueType.DeadLetterSubscription) { IsHidden = true }
            };
         topicDeadletterCommand.Add(topicSendCommand);
         topicDeadletterCommand.Add(readTopicCommand);
         topicDeadletterCommand.Add(peekCommand);


         var connectionCommand = new Command("connection", "Sets or clears a Service Bus Connection string. If no connection string is specified, Azure RBAC will be used.");
         var exitOption = new Option<bool>(new string[] { "--exit", "-e" }, () => false, "Forces close of app after method runs") { IsHidden = true };
         var connStringOption = new Option<string>(new string[] { "-c", "--connection-string" }, "Connection String to your Service Bus");
         connStringOption.IsRequired = true;
         var setConnectionCommand = new Command("set", "Sets the connection string for your target service bus")
            {
                connStringOption,
                exitOption
            };
         setConnectionCommand.Handler = CommandHandler.Create<string, bool>(Worker.SetConnectionString);
         var clearConnectionCommand = new Command("clear", "Clears a saved connection string");
         clearConnectionCommand.Handler = CommandHandler.Create(Worker.ClearConnectionString);
         connectionCommand.Add(setConnectionCommand);
         connectionCommand.Add(clearConnectionCommand);

         RootCommand rootCommand = new RootCommand(description: $"Utility to help you send and receive test messages to a Service Bus Queue or Topic/Subscription. " +
                 $"{Environment.NewLine}https://github.com/mmckechney/ServiceBusUtility");
         rootCommand.Add(connectionCommand);
         rootCommand.Add(queueCommand);
         rootCommand.Add(topicCommand);
         rootCommand.Add(queueDeadLetterCommand);
         rootCommand.Add(topicDeadletterCommand);

         var parser = new CommandLineBuilder(rootCommand)
           .UseDefaults()
           .UseHelp(ctx =>
           {
              ctx.HelpBuilder.CustomizeLayout(_ => HelpBuilder.Default
                                    .GetLayout()
                                    .Prepend(
                                        _ => AnsiConsole.Write(new FigletText("Service Bus Utility"))
                                    ));

           })
           .Build();

         return parser;
      }
   }
}
