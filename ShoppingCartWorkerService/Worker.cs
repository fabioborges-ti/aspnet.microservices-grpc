using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductGrpc.Protos;
using ShoppingCartGrpc.Protos;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShoppingCartWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _config = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                // 1 Create SC if not exist 
                // 2 Retrieve products from product grpc with server stream 
                // 3 Add SC items into SC with client stream 

                #region ShoppingCart

                using var scChannel = GrpcChannel.ForAddress
                    (_config.GetValue<string>("WorkerService:ShoppingCartServerUrl"));

                var scClient = new ShoppingCartProtoService.ShoppingCartProtoServiceClient(scChannel);


                // create SC if not exist 
                var scModel = await GetOrCreateShoppingCartAsync(scClient);

                // open sc client stream 
                using var scClientStream = scClient.AddItemIntoShoppingCart(cancellationToken: stoppingToken);

                #endregion

                #region Products

                using var productChannel = GrpcChannel.ForAddress
                    (_config.GetValue<string>("WorkerService:ProductServerUrl"));

                var productClient = new ProductProtoService.ProductProtoServiceClient(productChannel);

                _logger.LogInformation("GetAllProducts started...");

                // retrive products from products grpc with server stream 
                using var clientData = productClient.GetAllProducts(new GetAllProductsRequest(), cancellationToken: stoppingToken);

                await foreach (var responseData in clientData.ResponseStream.ReadAllAsync(cancellationToken: stoppingToken))
                {
                    _logger.LogInformation("GetAllProducts Stream Response: {responseData}", responseData);

                    // add sc items into SC with client stream 
                    var addNewScItem = new AddItemIntoShoppingCartRequest
                    {
                        Username = _config.GetValue<string>("WorkerService:UserName"),
                        DiscountCode = "CODE_100",
                        NewCartItem = new ShoppingCartItemModel
                        {
                            ProductId = responseData.ProductId,
                            Productname = responseData.Name,
                            Price = responseData.Price,
                            Color = "Black",
                            Quantity = 1
                        }
                    };

                    await scClientStream.RequestStream.WriteAsync(addNewScItem);

                    _logger.LogInformation("ShoppingCart Client Stream Added New item: {addNewScItem}", addNewScItem);
                }

                await scClientStream.RequestStream.CompleteAsync();

                var addItemIntoShoppingCartResponse = await scClientStream;

                _logger.LogInformation("AddItemIntoShoppingCart Client Stream Response: {addItemIntoShoppingCartResponse}", addItemIntoShoppingCartResponse);

                #endregion

                await Task.Delay(_config.GetValue<int>("WorkerService:TaskInterval"), stoppingToken);
            }
        }

        private async Task<ShoppingCartModel> GetOrCreateShoppingCartAsync(ShoppingCartProtoService.ShoppingCartProtoServiceClient scClient)
        {
            ShoppingCartModel shoppingCartModel;

            try
            {
                _logger.LogInformation("GetShoppingCartAsync started...");

                shoppingCartModel = await scClient.GetShoppingCartAsync(new GetShoppingCartRequest
                {
                    Username = _config.GetValue<string>("WorkerService:UserName")
                });

                _logger.LogInformation("GetShoppingCartAsync Response: {shoppingCartModel}", shoppingCartModel);
            }
            catch (RpcException ex)
            {
                if (ex.StatusCode == StatusCode.NotFound)
                {
                    _logger.LogInformation("CreateShoppingCartAsync started...");

                    shoppingCartModel = await scClient.CreateShoppingCartAsync(new ShoppingCartModel
                    {
                        Username = _config.GetValue<string>("WorkerService:UserName")
                    });

                    _logger.LogInformation("CreateShoppingCartAsync Response: {shoppingCartModel}", shoppingCartModel);
                }
                else
                {
                    throw ex;
                }
            }

            return shoppingCartModel;
        }
    }
}
