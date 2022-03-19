using DiscountGrpc.Data;
using DiscountGrpc.Protos;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace DiscountGrpc.Services
{
    public class DiscountService : DiscountProtoService.DiscountProtoServiceBase
    {
        private readonly ILogger<DiscountService> _logger;

        public DiscountService(ILogger<DiscountService> logger)
        {
            _logger = logger;
        }

        public override Task<DiscountModel> GetDiscount(GetDiscountRequest request,
            ServerCallContext context)
        {
            var discount = DiscountContext.Discounts.FirstOrDefault(o => o.Code == request.DiscountCode);

            _logger.LogInformation($"Discount is operated with the {discount.Code} code and the amount is {discount.Amount}");

            return Task.FromResult(new DiscountModel
            {
                DiscountId = discount.DiscountId,
                Code = discount.Code,
                Amount = discount.Amount
            });
        }
    }
}
