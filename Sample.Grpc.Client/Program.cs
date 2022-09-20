using Grpc.Net.Client;
using Sample.Grpc;

// The port number must match the HTTPS port in Sample.Grpc/Properties/launchSettings.json
using var channel = GrpcChannel.ForAddress("https://localhost:7124");

var client = new Greeter.GreeterClient(channel);
var reply = await client.SayHelloAsync(new HelloRequest { Name = "Sample.Grpc.Client" });
Console.WriteLine("Reply received from gRPC service: " + reply.Message);
Console.WriteLine("Press any key to exit...");
Console.ReadKey();