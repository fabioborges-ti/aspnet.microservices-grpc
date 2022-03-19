using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductGrpc.Protos;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProductWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;
        private readonly ProductFactory _factory;

        public Worker(ILogger<Worker> logger, IConfiguration configuration, ProductFactory factory)
        {
            _logger = logger;
            _config = configuration;
            _factory = factory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                using var channel = GrpcChannel.ForAddress(_config.GetValue<string>("WorkerService:ServerUrl"));

                var client = new ProductProtoService.ProductProtoServiceClient(channel);

                // await GetProductAsync(client);

                // await AddProductAsync(client, stoppingToken);

                _logger.LogInformation("AddProductAsync started");

                var addProductResponse = await client.AddProductAsync(await _factory.Generate(), cancellationToken: stoppingToken);

                _logger.LogInformation("AddProductAsync Response: {product}", addProductResponse.ToString());

                await Task.Delay(_config.GetValue<int>("WorkerService:TaskInterval"), stoppingToken);
            }
        }

        private static async Task GetProductAsync(ProductProtoService.ProductProtoServiceClient client)
        {
            Console.WriteLine("GetProductAsync started...");

            var response = await client.GetProductAsync(new GetProductRequest
            {
                ProductId = 1
            });

            Console.WriteLine("GetProductAsync Response: " + response.ToString());
        }

        private async Task AddProductAsync(ProductProtoService.ProductProtoServiceClient client, CancellationToken stoppingToken)
        {
            Console.WriteLine("AddProductAsync started...");

            var addProductResponse = await client.AddProductAsync(new AddProductRequest
            {
                Product = new ProductModel
                {
                    Name = $"{_config.GetValue<string>("WorkerService:ProductName")} - {DateTimeOffset.Now}",
                    Description = "New Red Phone Mi10T",
                    Price = 699,
                    Status = ProductStatus.Instock,
                    CreatedTime = Timestamp.FromDateTime(DateTime.UtcNow)
                }
            }, cancellationToken: stoppingToken);

            Console.WriteLine("AddProduct Response: " + addProductResponse.ToString());
        }
    }
}
