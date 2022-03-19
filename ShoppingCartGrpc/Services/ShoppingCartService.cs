using AutoMapper;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShoppingCartGrpc.Data;
using ShoppingCartGrpc.Models;
using ShoppingCartGrpc.Protos;
using System.Linq;
using System.Threading.Tasks;

namespace ShoppingCartGrpc.Services
{
    public class ShoppingCartService : ShoppingCartProtoService.ShoppingCartProtoServiceBase
    {
        private readonly ILogger<ShoppingCartService> _logger;
        private readonly IMapper _mapper;
        private readonly ShoppingCartContext _shoppingCartDbContext;
        private readonly DiscountService _discountService;

        public ShoppingCartService(ILogger<ShoppingCartService> logger, IMapper mapper, ShoppingCartContext shoppingCartDbContext, DiscountService discountService)
        {
            _logger = logger;
            _mapper = mapper;
            _shoppingCartDbContext = shoppingCartDbContext;
            _discountService = discountService;
        }

        public override async Task<ShoppingCartModel> GetShoppingCart(GetShoppingCartRequest request,
            ServerCallContext context)
        {
            var shoppingCart = await _shoppingCartDbContext.ShoppingCart
                .FirstOrDefaultAsync(item => item.UserName == request.Username);

            if (shoppingCart == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"ShoppingCart with Username={request.Username} not found"));
            }

            var shoppingCartModel = _mapper.Map<ShoppingCartModel>(shoppingCart);

            return shoppingCartModel;
        }

        public override async Task<ShoppingCartModel> CreateShoppingCart(ShoppingCartModel request,
            ServerCallContext context)
        {
            var shoppingCart = _mapper.Map<ShoppingCart>(request);

            var isExist = await _shoppingCartDbContext.ShoppingCart
                .AnyAsync(item => item.UserName == request.Username);

            if (isExist)
            {
                _logger.LogError("Invalid username for ShoppingCart creation. Username: {username}", shoppingCart.UserName);

                throw new RpcException(new Status(StatusCode.NotFound, $"ShoppingCart with Username={request.Username} is already exist."));
            }

            _shoppingCartDbContext.ShoppingCart.Add(shoppingCart);

            await _shoppingCartDbContext.SaveChangesAsync();

            _logger.LogInformation("ShoppingCart is successfully created. Username: {username}", shoppingCart.UserName);

            return _mapper.Map<ShoppingCartModel>(shoppingCart);
        }

        public override async Task<RemoveItemIntoShoppingCartResponse> RemoveItemIntoShoppingCart(RemoveItemIntoShoppingCartRequest request,
            ServerCallContext context)
        {
            var shoppingCart = await _shoppingCartDbContext.ShoppingCart
                .FirstOrDefaultAsync(item => item.UserName == request.Username);

            if (shoppingCart == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"ShoppingCart with username={request.Username} is not found."));
            }

            var removeCarItem = shoppingCart.Items
                .FirstOrDefault(i => i.ProductId == request.RemoveCartItem.ProductId);

            if (removeCarItem == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound,
                    $"CartItem with ProductId={request.RemoveCartItem.ProductId} is not found in the ShoppingCart."));
            }

            shoppingCart.Items.Remove(removeCarItem);

            var removeCount = await _shoppingCartDbContext.SaveChangesAsync();

            return new RemoveItemIntoShoppingCartResponse
            {
                Success = removeCount > 0
            };
        }

        public override async Task<AddItemIntoShoppingCartResponse> AddItemIntoShoppingCart(IAsyncStreamReader<AddItemIntoShoppingCartRequest> requestStream,
            ServerCallContext context)
        {
            while (await requestStream.MoveNext())
            {
                var shoppingCart = await _shoppingCartDbContext.ShoppingCart
                    .FirstOrDefaultAsync(item => item.UserName == requestStream.Current.Username);

                if (shoppingCart == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound,
                        $"ShoppingCart with UserName={requestStream.Current.Username} is not found."));
                }

                var newAddedCartItem = _mapper.Map<ShoppingCartItem>(requestStream.Current.NewCartItem);

                var cartItem = shoppingCart.Items.FirstOrDefault(i => i.ProductId == newAddedCartItem.ProductId);

                if (cartItem != null)
                {
                    cartItem.Quantity++;
                }
                else
                {
                    var discount = await _discountService.GetDiscount(requestStream.Current.DiscountCode);

                    newAddedCartItem.Price -= discount.Amount;

                    shoppingCart.Items.Add(newAddedCartItem);
                }
            }

            var insertCount = await _shoppingCartDbContext.SaveChangesAsync();

            return new AddItemIntoShoppingCartResponse
            {
                Success = insertCount > 0,
                InsertCount = insertCount
            };
        }
    }
}
