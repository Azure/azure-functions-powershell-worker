using System;

namespace Microsoft.Azure.Functions.PowerShellWorker
{
    public class StartupArguments
    {
        public int GrpcMaxMessageLength { get; set; }
        public string Host {get; set;}
        public int Port {get; set;}
        public string RequestId {get; set;}
        public string WorkerId {get; set;}

        public static StartupArguments Parse(string[] args)
        {
            StartupArguments arguments = new StartupArguments();
            for (int i = 1; i < 10; i+=2)
            {
                string currentArg = args[i];
                switch (i)
                {
                    case 1: arguments.Host = currentArg; break;
                    case 3: arguments.Port = int.Parse(currentArg); break;
                    case 5: arguments.WorkerId = currentArg; break;
                    case 7: arguments.RequestId = currentArg; break;
                    case 9: arguments.GrpcMaxMessageLength = int.Parse(currentArg); break;
                    default: throw new InvalidOperationException();
                }
            }

            Console.WriteLine($"host: {arguments.Host}");
            Console.WriteLine($"port: {arguments.Port}");
            Console.WriteLine($"workerId: {arguments.WorkerId}");
            Console.WriteLine($"requestId: {arguments.RequestId}");
            Console.WriteLine($"grpcMaxMessageLength: {arguments.GrpcMaxMessageLength}");

            return arguments;
        }
    }
}