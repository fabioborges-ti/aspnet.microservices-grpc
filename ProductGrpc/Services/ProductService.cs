using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductGrpc.Data;
using ProductGrpc.Models;
using ProductGrpc.Protos;
using System.Threading.Tasks;

namespace ProductGrpc.Services
{
    public class ProductService : ProductProtoService.ProductProtoServiceBase
    {
        private readonly ILogger<ProductService> _logger;
        private readonly IMapper _mapper;
        private readonly ProductsContext _productsContext;

        public ProductService(ILogger<ProductService> logger, IMapper mapper, ProductsContext productsContext)
        {
            _logger = logger;
            _mapper = mapper;
            _productsContext = productsContext;
        }

        public override Task<Empty> Test(Empty request, ServerCallContext context)
        {
            return base.Test(request, context);
        }

        public override async Task<ProductModel> GetProduct(GetProductRequest request, ServerCallContext context)
        {
            var product = await _productsContext.Product.FindAsync(request.ProductId);

            if (product == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Product with ID {request.ProductId} is not found"));
            }

            var productModel = _mapper.Map<ProductModel>(product);

            return productModel;
        }

        public override async Task GetAllProducts(GetAllProductsRequest request, IServerStreamWriter<ProductModel> responseStream, ServerCallContext context)
        {
            var productList = await _productsContext.Product.ToListAsync();

            foreach (var product in productList)
            {
                var productModel = _mapper.Map<ProductModel>(product);

                await responseStream.WriteAsync(productModel);
            }
        }

        public override async Task<ProductModel> AddProduct(AddProductRequest request, ServerCallContext context)
        {
            var product = _mapper.Map<Product>(request.Product);

            _productsContext.Product.Add(product);

            await _productsContext.SaveChangesAsync();

            _logger.LogInformation("Product successfully added: {productId}_{productName}", product.ProductId, product.Name);

            var productModel = _mapper.Map<ProductModel>(product);

            return productModel;
        }

        public override async Task<ProductModel> UpdateProduct(UpdateProductRequest request, ServerCallContext context)
        {
            var product = _mapper.Map<Product>(request.Product);

            var isExist = await _productsContext.Product.AnyAsync(p => p.ProductId == product.ProductId);

            if (!isExist)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Product with ID {product.ProductId} is not found"));
            }

            _productsContext.Entry(product).State = EntityState.Modified;

            try
            {
                await _productsContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw;
            }

            var productModel = _mapper.Map<ProductModel>(product);

            return productModel;
        }

        public override async Task<DeleteProductResponse> DeleteProduct(DeleteProductRequest request, ServerCallContext context)
        {
            var product = await _productsContext.Product.FindAsync(request.ProductId);

            if (product == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"Product with ID {request.ProductId} is not found"));
            }

            _productsContext.Product.Remove(product);

            var deleteCount = await _productsContext.SaveChangesAsync();

            return new DeleteProductResponse
            {
                Success = deleteCount > 0
            };
        }

        public override async Task<InsertBulkProductResponse> InsertBulkProduct(IAsyncStreamReader<ProductModel> requestStream, ServerCallContext context)
        {
            while (await requestStream.MoveNext())
            {
                var product = _mapper.Map<Product>(requestStream.Current);

                _productsContext.Product.Add(product);
            }

            var insertCount = await _productsContext.SaveChangesAsync();

            return new InsertBulkProductResponse
            {
                Success = insertCount > 0,
                InsertCount = insertCount
            };
        }
    }
}
