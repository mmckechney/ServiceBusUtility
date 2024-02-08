using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Text;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.VisualBasic;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Linq;
using Azure.Messaging.ServiceBus.Administration;
namespace ServiceBusUtility
{
   internal class Worker : BackgroundService
   {
      private static Parser rootParser;
      private static StartArgs startArgs;
      private static ILogger<Worker> log;
      private static IConfiguration config;
      private static ServiceBusSettings serviceBusSettings;

      private static int messagesReceived = 0;
      private static ServiceBusSender _sender = null;
      private static ServiceBusClient _client = null;
      private static ServiceBusAdministrationClient _adminClient;
      private static string connectionString = "";
      private static string activeQueueOrTopic = "";
      static CancellationTokenSource cts = new CancellationTokenSource();


      public Worker(ILogger<Worker> logger, IConfiguration configuration, StartArgs sArgs, ServiceBusSettings sbSetting)
      {
         startArgs = sArgs;
         log = logger;
         config = configuration;
         serviceBusSettings = sbSetting;
      }
      protected async override Task ExecuteAsync(CancellationToken stoppingToken)
      {
         connectionString = GetConnectionString();
         Directory.SetCurrentDirectory(Path.GetDirectoryName(System.AppContext.BaseDirectory));
         rootParser = CommandBuilder.BuildCommandLine();
         string[] args = startArgs.Args;
         if (args.Length == 0) args = new string[] { "-h" };
         int val = await rootParser.InvokeAsync(args);

         while (true)
         {
            cts = new CancellationTokenSource();
            messagesReceived = 0;
            var connStringStatus = string.IsNullOrEmpty(connectionString) ? "No connection string set (Azure RBAC will be used)" : "Connection string set";
            log.LogInformation("");
            log.LogInformation("---------------------------------");
            log.LogInformation("Current Service Bus settings (used command flags to change):", ConsoleColor.DarkCyan);
            log.LogInformation(new() { { "Service Bus Namespace: ",ConsoleColor.DarkYellow},{ serviceBusSettings.SbNamespace, ConsoleColor.Yellow} });
            log.LogInformation(new() { { "Queue: ", ConsoleColor.DarkYellow }, { serviceBusSettings.QueueName, ConsoleColor.Yellow } });
            log.LogInformation(new() { { "Topic: ", ConsoleColor.DarkYellow }, { serviceBusSettings.TopicName, ConsoleColor.Yellow } });
            log.LogInformation(new() { { "Subscription: ", ConsoleColor.DarkYellow }, { serviceBusSettings.SubscriptionName, ConsoleColor.Yellow } });
            log.LogInformation(new() { { "Message Handling: " , ConsoleColor.DarkYellow}, { serviceBusSettings.MessageHandling.ToString(), ConsoleColor.Yellow } });
            log.LogInformation(new() { { "Connection String: ", ConsoleColor.DarkYellow }, { connStringStatus, ConsoleColor.Yellow } });
            log.LogInformation("---------------------------------");
            await CountMessages();
            log.LogInformation("---------------------------------");
            Console.Write("sbu> ");
            Console.ResetColor();
            var line = Console.ReadLine();
            if (line == null)
            {
               return;
            }
            if (line.Length == 0) line = "-h";
            val = await rootParser.InvokeAsync(line);
         }
      }



      static ServiceBusClient Client
      {
         get
         {
            if (_client == null)
            {
               if (string.IsNullOrEmpty(connectionString) && !string.IsNullOrWhiteSpace(serviceBusSettings.SbNamespace))
               {
                  if (!serviceBusSettings.SbNamespace.ToLower().EndsWith("servicebus.windows.net"))
                  {
                     serviceBusSettings.SbNamespace = serviceBusSettings.SbNamespace + ".servicebus.windows.net";
                  }
                  log.LogInformation("Using Azure RBAC authentication to connect to service bus...", ConsoleColor.Yellow);

                  _client = new ServiceBusClient(serviceBusSettings.SbNamespace, new AzureCliCredential());
               }
               else if (!string.IsNullOrEmpty(connectionString))
               {
                  log.LogInformation("Using saved connection string to connect to service bus...", ConsoleColor.Yellow);
                  _client = new ServiceBusClient(connectionString);

               }
               else
               {
                  throw new ArgumentException("connectionString", "connectionString");
               }
            }
            return _client;
         }
      }
      static ServiceBusAdministrationClient AdminClient
      {
         get
         {
            if (_adminClient == null)
            {
               if (string.IsNullOrEmpty(connectionString) && !string.IsNullOrWhiteSpace(serviceBusSettings.SbNamespace))
               {
                  if (!serviceBusSettings.SbNamespace.ToLower().EndsWith("servicebus.windows.net"))
                  {
                     serviceBusSettings.SbNamespace = serviceBusSettings.SbNamespace + ".servicebus.windows.net";
                  }
                  log.LogInformation("Using Azure RBAC authentication to connect to service bus...", ConsoleColor.Yellow);

                  _adminClient = new ServiceBusAdministrationClient(serviceBusSettings.SbNamespace, new AzureCliCredential());
               }
               else if (!string.IsNullOrEmpty(connectionString))
               {
                  log.LogInformation("Using saved connection string to connect to service bus...", ConsoleColor.Yellow);
                  _adminClient = new ServiceBusAdministrationClient(connectionString);

               }
               else
               {
                  throw new ArgumentException("connectionString", "connectionString");
               }
            }
            return _adminClient;
         }
      }
      static ServiceBusSender Sender
      {
         get
         {
            if (_sender == null || _sender.EntityPath != activeQueueOrTopic)
            {
               if (_sender != null) _sender.DisposeAsync().GetAwaiter().GetResult();
               if (string.IsNullOrWhiteSpace(activeQueueOrTopic)) activeQueueOrTopic = serviceBusSettings.QueueName;
               _sender = Client.CreateSender(activeQueueOrTopic);
            }
            return _sender;
         }
      }
      static ServiceBusReceiver GetReceiver(QueueType queueType)
      {

         switch (queueType)
         {

            case QueueType.TopicSubscription:
               return Client.CreateReceiver(serviceBusSettings.TopicName, serviceBusSettings.SubscriptionName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });
            case QueueType.TopicScheduled:
               return Client.CreateReceiver(serviceBusSettings.TopicName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });
            case QueueType.DeadLetterQueue:
               return Client.CreateReceiver(serviceBusSettings.QueueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock, SubQueue = SubQueue.DeadLetter });
            case QueueType.DeadLetterSubscription:
               return Client.CreateReceiver(serviceBusSettings.TopicName, serviceBusSettings.SubscriptionName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock, SubQueue = SubQueue.DeadLetter });
            case QueueType.Queue:
            default:
               return Client.CreateReceiver(serviceBusSettings.QueueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });
         }
      }
      private static void IncrementMessagesReceived()
      {
         Interlocked.Increment(ref messagesReceived);
      }
      static (ServiceBusProcessor, string) GetProcessor(QueueType queueType)
      {
         var options = new ServiceBusProcessorOptions
         {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 2
         };

         string message;
         ServiceBusProcessor processor = null;
         switch (queueType)
         {

            case QueueType.TopicSubscription:
               message = $"Starting message read for topic/subscription '{serviceBusSettings.TopicName}/{serviceBusSettings.SubscriptionName}'";
               processor = Client.CreateProcessor(serviceBusSettings.TopicName, serviceBusSettings.SubscriptionName, options);
               break;
            case QueueType.DeadLetterSubscription:
               options.SubQueue = SubQueue.DeadLetter;
               message = $"Starting message read for topic/subscription '{serviceBusSettings.TopicName}/{serviceBusSettings.SubscriptionName}' deadletter sub-queue";
               processor = Client.CreateProcessor(serviceBusSettings.TopicName, serviceBusSettings.SubscriptionName, options);
               break;
            case QueueType.DeadLetterQueue:
               options.SubQueue = SubQueue.DeadLetter;
               message = $"Starting message read for queue '{serviceBusSettings.QueueName}' deadletter sub-queue";
               processor = Client.CreateProcessor(serviceBusSettings.QueueName, options);
               break;
            case QueueType.Queue:
            default:
               message = $"Starting message read for queue '{serviceBusSettings.QueueName}'";
               processor = Client.CreateProcessor(serviceBusSettings.QueueName, options);
               break;
         }
         return (processor, message);
      }


      internal static async Task PeekMessages(int count, QueueType queueType, bool scheduled, ServiceBusSettings sbSettings)
      {
         MergeServiceBusSettings(sbSettings);
         if (queueType == QueueType.TopicSubscription && scheduled)
         {
            queueType = QueueType.TopicScheduled;
         }
         ServiceBusReceiver receiver = GetReceiver(queueType);

         var msgs = await receiver.PeekMessagesAsync(count);
         var num = 1;
         foreach (var m in msgs)
         {
            WriteMessageOutput(m, num);
            num++;
         }
         log.LogInformation("----------");
         log.LogInformation($"{msgs.Count} messages peeked");
      }

      internal static async Task SendMessages(int count, int wait, int delay, QueueType queueType, ServiceBusSettings sbSettings, bool quiet)
      {
         MergeServiceBusSettings(sbSettings);
         Stopwatch s = new Stopwatch();
         List<Task<(bool success, string result)>> msgs = new();
         try
         {
            log.LogInformation($"Starting send of {count} messages...");

            Worker.activeQueueOrTopic = queueType == QueueType.Queue ? serviceBusSettings.QueueName : serviceBusSettings.TopicName;
            s.Start();

            if (wait == 0)
            {

               for (int i = 0; i < count; i++)
               {
                  msgs.Add(SendMessage(i, delay, quiet, cts.Token));
               }
               Task.WaitAll(msgs.ToArray());

               if (!quiet)
               {
                  foreach (var t in msgs)
                  {
                     if (t.Result.result.Length > 0)
                     {
                        log.LogInformation(t.Result.result);
                     }
                  }
               }
            }
            else
            {

               for (int i = 0; i < count; i++)
               {
                  var output = await SendMessage(i, delay, quiet, cts.Token);
                  if (!output.success) log.LogError("Error sending message.");
                  log.LogInformation(output.result);
                  Thread.Sleep(wait);
               }
            }
            s.Stop();
            var succeeded = msgs.Count(m => m.Result.success);
            var failed = msgs.Count(m => !m.Result.success);
            log.LogInformation($"Sent {succeeded} messages in {s.Elapsed.TotalSeconds.ToString("#.##")} seconds. {failed} messages failed.");
         }
         catch (Exception exe)
         {
            HandleServiceBusException(exe);
         }
      }

      internal static async Task CountMessages()
      {
         var color = ConsoleColor.Blue;
         var numColor = ConsoleColor.DarkGray;
         if (!string.IsNullOrWhiteSpace(serviceBusSettings.QueueName))
         {
            var queueInfo = await AdminClient.GetQueueRuntimePropertiesAsync(serviceBusSettings.QueueName);
            log.LogInformation(new() { { $"Queue Active Messages: ", color }, { queueInfo.Value.ActiveMessageCount.ToString(), numColor } });
            log.LogInformation(new() { { $"Queue Scheduled Messages: ", color }, { queueInfo.Value.ScheduledMessageCount.ToString(), numColor } });
            log.LogInformation(new() { { $"Queue DeadLetter Messages: ", color }, { queueInfo.Value.DeadLetterMessageCount.ToString(), numColor } });
         }
         if (!string.IsNullOrWhiteSpace(serviceBusSettings.SubscriptionName) && !string.IsNullOrWhiteSpace(serviceBusSettings.TopicName))
         {
            var subInfo = await AdminClient.GetSubscriptionRuntimePropertiesAsync(serviceBusSettings.TopicName,serviceBusSettings.SubscriptionName);
            log.LogInformation(new() { { $"Subscription Active Messages: ", color }, { subInfo.Value.ActiveMessageCount.ToString(), numColor } });
            log.LogInformation(new() { { $"Subscription DeadLetter Messages: ", color }, { subInfo.Value.DeadLetterMessageCount.ToString(), numColor } });
         }
      }
   internal static async Task<(bool success, string result)> SendMessage(int counter, int delay, bool quiet, CancellationToken cancelToken)
      {
         try
         {
            var sbMessages = new ServiceBusMessage($"This is a new test message for queue created at {DateTime.UtcNow}");
            string id = Guid.NewGuid().ToString();
            sbMessages.MessageId = id;

            if (delay != 0)
            {
               sbMessages.ScheduledEnqueueTime = DateTime.UtcNow.AddSeconds(delay);
               var sequenceNumber = await Sender.ScheduleMessageAsync(sbMessages, sbMessages.ScheduledEnqueueTime, cancelToken);
               if (!quiet)
               {
                  return (true, $"Loop {(counter + 1).ToString().PadLeft(4, '0')}: Sent message '{id}' to '{activeQueueOrTopic}'. Sequence #{sequenceNumber} scheduled for {sbMessages.ScheduledEnqueueTime.ToString("yyyy-MM-dd HH:mm:ss fff")}");
               }
            }
            else
            {
               await Sender.SendMessageAsync(sbMessages, cancelToken);
               if (!quiet)
               {
                  return (true, $"Loop {(counter + 1).ToString().PadLeft(4, '0')}: Sent message '{id}' to '{activeQueueOrTopic}'.");
               }
            }
            return (false, string.Empty);
         }
         catch (Exception exe)
         {
            HandleServiceBusException(exe, cancelToken);
            return (false, string.Empty);
         }
      }

      internal static async Task ReadMessage(long? sequenceNumber, QueueType queueType, int delay, bool scheduled, ServiceBusSettings sbSettings)
      {
         try
         {
            MergeServiceBusSettings(sbSettings);
            Worker.activeQueueOrTopic = queueType == QueueType.Queue ? serviceBusSettings.QueueName : serviceBusSettings.TopicName;
            if (!sequenceNumber.HasValue)
            {
               await InitializeReceiver(queueType);
               log.LogInformation($"Total Messages Received: {messagesReceived}", ConsoleColor.Cyan);
            }
            else
            {
               await ReadSpecificMessage(sequenceNumber.Value, queueType, serviceBusSettings.MessageHandling, delay, scheduled);
            }
         }
         catch (Exception exe)
         {
            HandleServiceBusException(exe);
         }
         return;
      }
      private static async Task ReadSpecificMessage(long sequenceNumber, QueueType queueType, MessageHandling handling, int delay, bool scheduled)
      {

         if (queueType == QueueType.TopicSubscription && scheduled)
         {
            queueType = QueueType.TopicScheduled;
         }
         ServiceBusReceivedMessage message = null;
         ServiceBusReceiver receiver = GetReceiver(queueType);

         try
         {
            message = await receiver.ReceiveDeferredMessageAsync(sequenceNumber);
         }
         catch (ServiceBusException exe)
         {

            log.LogError(exe.Message);
            log.LogInformation("Trying to peek for the message");
            message = await receiver.PeekMessageAsync(sequenceNumber);
         }
         if (message != null)
         {
            WriteMessageOutput(message, 1);

            if (message.State == ServiceBusMessageState.Active)
            {
               log.LogInformation($"Because message #{message.SequenceNumber} is Active and Peeked, no action can be taken.");
               return;
            }
            switch (serviceBusSettings.MessageHandling)
            {
               case MessageHandling.Complete:

                  switch (message.State)
                  {
                     case ServiceBusMessageState.Scheduled:
                        log.LogInformation("Message handling set to Complete. Finishing message processing. Scheduled message has been cancelled");
                        await Sender.CancelScheduledMessageAsync(message.SequenceNumber);
                        break;
                     case ServiceBusMessageState.Deferred:
                        log.LogInformation("Message handling set to Complete. Finishing message processing. Deferred message getting removed from queue");
                        await receiver.CompleteMessageAsync(message);
                        break;
                  }
                  break;
               case MessageHandling.DeadLetter:
                  log.LogInformation("Message handling set to DeadLetter. Stopping message processing, message going to DeadLetter Queue");
                  await receiver.DeadLetterMessageAsync(message, "Deadletter by request");
                  break;
               case MessageHandling.Abandon:
                  log.LogInformation("Message handling set to Abandon. Stopping message processing, putting back into queue with Deliver Count +1");
                  await receiver.AbandonMessageAsync(message);
                  break;
               case MessageHandling.Defer:
                  log.LogInformation($"Message with sequence #{message.SequenceNumber} is getting deferred");
                  await receiver.DeferMessageAsync(message);
                  break;
               case MessageHandling.Reschedule:
                  if (delay == 0) delay = 60;
                  var schedule = DateTime.UtcNow.AddSeconds(delay);
                  var newMsg = message.Clone();
                  newMsg.ScheduledEnqueueTime = schedule;
                  var newSequenceNumber = await Sender.ScheduleMessageAsync(newMsg, schedule);
                  log.LogInformation($"Rescheduled message with sequence #{message.SequenceNumber} as new messages with sequence #{newSequenceNumber} scheduled for {schedule}");
                  switch (message.State)
                  {
                     case ServiceBusMessageState.Active:
                        break;
                     case ServiceBusMessageState.Scheduled:
                        await Sender.CancelScheduledMessageAsync(message.SequenceNumber);
                        break;
                     case ServiceBusMessageState.Deferred:
                        await receiver.CompleteMessageAsync(message);
                        break;
                  }
                  log.LogInformation($"Marking original message with sequence #{message.SequenceNumber} as Complete");
                  break;
               case MessageHandling.None:
               default:
                  log.LogInformation($"Message Handling set to None. No action taken on message");
                  break;
            }
         }
         else
         {
            log.LogInformation($"Unable to find message with Sequence Number {sequenceNumber}");
         }
      }

      private static void WriteMessageOutput(ServiceBusReceivedMessage message, int messageNum)
      {
         StringBuilder sb = new StringBuilder();
         var amqp = message.GetRawAmqpMessage();
         sb.AppendLine("");
         sb.AppendLine("----------");
         sb.AppendLine($"Sequence Number: {message.SequenceNumber}");
         sb.AppendLine($"Message State:\t {message.State.ToString()}");
         sb.AppendLine($"Message body:\t {Encoding.UTF8.GetString(message.Body)}");
         sb.AppendLine($"Message id:\t {message.MessageId}");
         sb.AppendLine($"ScheduledTime:\t {message.ScheduledEnqueueTime}");
         sb.AppendLine($"EnqueuedTime:\t {message.EnqueuedTime}");
         sb.AppendLine($"ExpiresAt:\t {amqp.Properties.AbsoluteExpiryTime}");
         sb.AppendLine($"TimeToLive:\t {message.TimeToLive.TotalMinutes}");
         sb.AppendLine($"DeliveryCount:\t {amqp.Header.DeliveryCount}");
         sb.Append($"Messages rec'd:\t {messageNum}");
         log.LogInformation(sb.ToString());
      }

      private static void HandleServiceBusException(Exception exe)
      {
         HandleServiceBusException(exe, null);
      }
      private static void HandleServiceBusException(Exception exe, CancellationToken? token)
      {
         if (token != null && token.Value.IsCancellationRequested)
         {
            return;
         }
         cts.Cancel();
         if (exe is ServiceBusException)
         {
            var sbe = (ServiceBusException)exe;
            if (sbe.InnerException != null && sbe.InnerException is SocketException)
            {
               var se = (SocketException)sbe.InnerException;
               if (se.SocketErrorCode == SocketError.HostNotFound)
               {
                  log.LogError("Unable to find Service Bus namespace. Please validate that you have entered the correct value or have set the proper connection string");
               }
            }
            else if (sbe.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
               log.LogError("The specified queue or topic does not exist in the namespace. Please change the value and try again.");
            }

         }
         else if (exe is UnauthorizedAccessException)
         {
            log.LogError(new() { { $"You do not have send/receive RBAC access to this Service Bus.{Environment.NewLine}Please make sure your identity has ", ConsoleColor.White }, { "'Azure Service Bus Data Receiver' and 'Azure Service Bus Data Sender'", ConsoleColor.DarkGreen }, { $" role assignments{Environment.NewLine}To use a connection string, remove the `--ns` argument and set a connection string with 'sbu connection set' command", ConsoleColor.White } });
         }
         else if (exe is ArgumentException)
         {
            var aex = (ArgumentException)exe;
            if (aex.ParamName == "connectionString")
            {
               log.LogError("Please either specify a Service Bus Namespace argument value (--ns) or set a connection string with 'sbu connection set' command");
            }
            else if (aex.ParamName == "entityPath")
            {
               log.LogError("Please specify an appropriate Queue (-q) or Topic (-t) argument value");
            }
            else
            {
               log.LogError(aex.Message);
            }
         }
         else
         {
            log.LogError(exe.Message);
         }
      }

      static async Task InitializeReceiver(QueueType queueType)
      {
         (var processor, var message) = GetProcessor(queueType);

         log.LogInformation(message, ConsoleColor.Cyan);
         log.LogInformation("Press any key to stop reading and exit...", ConsoleColor.Cyan);

         processor.ProcessMessageAsync += ProcessMessageAsync;
         processor.ProcessErrorAsync += ExceptionReceivedHandler;

         var processTask = processor.StartProcessingAsync(cts.Token);
         while (true)
         {
            Console.ReadKey(true);
            await processor.StopProcessingAsync();
            break;
         }

      }
      private static async Task ProcessMessageAsync(ProcessMessageEventArgs arg)
      {
         var message = arg.Message;
         IncrementMessagesReceived();
         int msgNum = messagesReceived;
         WriteMessageOutput(message, msgNum);
         switch (serviceBusSettings.MessageHandling)
         {
            case MessageHandling.Complete:
               log.LogInformation("Finishing message processing. Message handling set to Complete and removed from queue");
               await arg.CompleteMessageAsync(message, arg.CancellationToken);
               break;
            case MessageHandling.DeadLetter:
               log.LogInformation("Message handling set to DeadLetter. Stopping message processing, message going to DeadLetter Queue");
               await arg.DeadLetterMessageAsync(message, "Deadletter by request");
               break;
            case MessageHandling.Abandon:
               log.LogInformation("Message handling set to Abandon. Stopping message processing, putting back into queue with Deliver Count +1");
               await arg.AbandonMessageAsync(message);
               break;
            case MessageHandling.Defer:
               var schedule = DateAndTime.Now.AddSeconds(30);
               log.LogInformation($"Message with sequence #{message.SequenceNumber} is getting deferred.");
               await arg.DeferMessageAsync(message);
               break;
         }

      }
      static Task ExceptionReceivedHandler(ProcessErrorEventArgs args)
      {
         HandleServiceBusException(args.Exception);
         log.LogError($"Message handler encountered an exception {args.Exception}.");
         return Task.CompletedTask;
      }

      private static void MergeServiceBusSettings(ServiceBusSettings sbSetting)
      {
         if(sbSetting.SbNamespace != "")
         {
            Worker.serviceBusSettings.SbNamespace = sbSetting.SbNamespace;
         }
         if(sbSetting.TopicName != "")
         {
            Worker.serviceBusSettings.TopicName = sbSetting.TopicName;
         }
         if(sbSetting.SubscriptionName != "")
         {
            Worker.serviceBusSettings.SubscriptionName = sbSetting.SubscriptionName;
         }
         if(sbSetting.QueueName != "")
         {
            Worker.serviceBusSettings.QueueName = sbSetting.QueueName;
         }
         if(sbSetting.MessageHandling != MessageHandling.Complete)
         {
            Worker.serviceBusSettings.MessageHandling = sbSetting.MessageHandling;
         }
      }
      internal static string GetConnectionString()
      {
         var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
         var csFile = Path.Combine(appDataPath, "cs.txt");
         if (File.Exists(csFile))
         {
            return File.ReadAllText(csFile).DecodeBase64();
         }
         else
         {
            return "";
         }

      }
      internal static void SetConnectionString(string connectionString, bool exit)
      {
         var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
         var csFile = Path.Combine(appDataPath, "cs.txt");
         File.WriteAllText(csFile, connectionString.EncodeBase64());
         Worker.connectionString = connectionString;
         if(exit)
         { Environment.Exit(0); }
      }
      internal static void ClearConnectionString(object obj)
      {
         var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
         var csFile = Path.Combine(appDataPath, "cs.txt");
         if (File.Exists(csFile))
         {
            File.Delete(csFile);
         }
         connectionString = "";
      }
   }
}
