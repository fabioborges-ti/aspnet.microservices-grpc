﻿using ShoppingCartGrpc.Models;
using System.Collections.Generic;
using System.Linq;

namespace ShoppingCartGrpc.Data
{
    public class ShoppingCartContextSeed
    {
        public static void SeedAsync(ShoppingCartContext shoppingCartContext)
        {
            if (!shoppingCartContext.ShoppingCart.Any())
            {
                var shoppingCarts = new List<ShoppingCart>
                {
                    new ShoppingCart
                    {
                        UserName = "swn",
                        Items = new List<ShoppingCartItem>
                        {
                            new ShoppingCartItem
                            {
                                ProductId = 1,
                                ProductName = "Mi10T",
                                Quantity = 2,
                                Color = "Black",
                                Price = 699
                            },
                            new ShoppingCartItem
                            {
                                ProductId = 2,
                                ProductName = "P40",
                                Quantity = 3,
                                Color = "Red",
                                Price = 899
                            }
                        }
                    }
                };

                shoppingCartContext.ShoppingCart.AddRange(shoppingCarts);
                shoppingCartContext.SaveChanges();
            }
        }
    }
}
