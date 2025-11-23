
using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using CSharpServer.Protos;

namespace CSharpServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Connect to the Android device
            // IMPORTANT: User needs to run 'adb forward tcp:50052 tcp:50052'
            Console.WriteLine("Connecting to Android Server on localhost:50052...");
            using var channel = GrpcChannel.ForAddress("http://localhost:50052");
            var client = new DataTransferService.DataTransferServiceClient(channel);

            Console.WriteLine("Connected!");
            Console.WriteLine("Available commands: 'contacts', 'logs', 'exit'");

            while (true)
            {
                Console.Write("\nEnter command: ");
                var input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input)) continue;
                if (input == "exit") break;

                try
                {
                    if (input == "contacts")
                    {
                        Console.WriteLine("Requesting contacts...");
                        var reply = await client.GetInfoAsync(new InfoRequest { Type = InfoType.Contacts });
                        
                        if (reply.DataCase == InfoResponse.DataOneofCase.Contacts)
                        {
                            Console.WriteLine($"Received {reply.Contacts.Contacts.Count} contacts:");
                            foreach (var contact in reply.Contacts.Contacts)
                            {
                                Console.WriteLine($" - {contact.Name}: {contact.PhoneNumber}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Server Message: {reply.Message}");
                        }
                    }
                    else if (input == "logs")
                    {
                        Console.WriteLine("Requesting call logs...");
                        var reply = await client.GetInfoAsync(new InfoRequest { Type = InfoType.CallLogs });
                        
                        if (reply.DataCase == InfoResponse.DataOneofCase.CallLogs)
                        {
                            Console.WriteLine($"Received {reply.CallLogs.Logs.Count} call logs:");
                            foreach (var log in reply.CallLogs.Logs)
                            {
                                Console.WriteLine($" - {log.Number} ({log.Type}) {log.Duration}s");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Server Message: {reply.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unknown command. Try 'contacts' or 'logs'.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error communicating with device: {ex.Message}");
                    Console.WriteLine("Make sure the Android app is running 'Start Server' and you ran 'adb forward tcp:50052 tcp:50052'");
                }
            }
        }
    }
}
