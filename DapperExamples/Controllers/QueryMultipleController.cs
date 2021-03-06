﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Dapper;
using DapperExamples.Model;
using DapperExamples.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace DapperExamples.Controllers
{
    [Route("[controller]")]
    public class QueryMultipleController : ControllerBase
    {
        private readonly IConfiguration configuration;

        public QueryMultipleController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        /// <summary>
        /// Get product by Id
        /// </summary>
        [HttpGet]
        [Route("GetProductAndOrderByProductId"), ProducesResponseType(typeof(Product), (int)HttpStatusCode.OK)]
        public IActionResult GetProductAndOrderByProductId(int id)
        {
            Product produto = null;
            using (var cn = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                cn.Open();

                var query = @"
SELECT * FROM Products WHERE ProductID = @Id
SELECT * FROM  [Order Details] WHERE ProductID = @Id
";
                using (var result = cn.QueryMultiple(query, new { id }))
                {
                    produto = result.Read<Product>().Single();
                    produto.OrderDetails = result.Read<OrderDetail>().ToList();
                }
                return Ok(produto);
            }
        }

        /// <summary>
        /// Get products by name
        /// </summary>
        [HttpGet]
        [Route("GetProductsByName")]
        public IActionResult GetProductsByName(string name = "chef")
        {
            List<Product> produto = null;
            name = $"%{name}%";
            using (var cn = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                cn.Open();

                var query = @"
SELECT * FROM Products WHERE ProductName like @name
SELECT  [Order Details].* FROM [Order Details]  JOIN Products ON [Order Details].ProductId = Products.ProductId WHERE Products.ProductName like @name
";
                using (var result = cn.QueryMultiple(query, new { name }))
                {
                    produto = result.Read<Product>().ToList();
                    produto.Map(result.Read<OrderDetail>(), prod => prod.ProductId, order => order.ProductId, (prod, details) => prod.OrderDetails = details.ToList());
                }
                return Ok(produto);
            }
        }

        /// <summary>
        /// Test performance using QueryMultiple
        /// </summary>
        [HttpGet]
        [Route("QueryMultiplePerformance")]
        public IActionResult QueryMultiplePerformance()
        {
            var name = $"%chef%";
            var stop = Stopwatch.StartNew();

            for (int i = 0; i < 5000; i++)
            {

                using (var cn = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
                {
                    cn.Open();

                    var query = @"
SELECT * FROM Products WHERE ProductName like @name
SELECT  [Order Details].* FROM  [Order Details] JOIN Products ON  [Order Details].ProductId = Products.ProductId WHERE Products.ProductName like @name
";
                    using (var result = cn.QueryMultiple(query, new { name }))
                    {
                        var produto = result.Read<Product>().ToList();
                        produto.Map(result.Read<OrderDetail>(), prod => prod.ProductId, order => order.ProductId, (prod, details) => prod.OrderDetails = details.ToList());
                    }
                }
            }

            stop.Stop();
            return Ok(stop.ElapsedMilliseconds);
        }

        /// <summary>
        /// Test performance using MultiMapping
        /// </summary>
        [HttpGet]
        [Route("MultiMappingMultiplePerformance")]
        public IActionResult MultiMappingMultiplePerformance()
        {
            var name = $"%chef%";
            var stop = Stopwatch.StartNew();

            for (int i = 0; i < 5000; i++)
            {

                using (var cn = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
                {
                    cn.Open();

                    var productDictionary = new Dictionary<int, Product>();
                    var query = @"SELECT * FROM Products JOIN  [Order Details] ON  [Order Details].ProductId = Products.ProductId WHERE Products.ProductName like @name";
                    var list = cn.Query<Product, OrderDetail, Product>(
                            query,
                            (product, orderDetail) =>
                            {
                                if (!productDictionary.TryGetValue(product.ProductId, out var productEntry))
                                {
                                    productEntry = product;
                                    productEntry.OrderDetails = new List<OrderDetail>();
                                    productDictionary.Add(productEntry.ProductId, productEntry);
                                }
                                productEntry.OrderDetails.Add(orderDetail);
                                return productEntry;
                            },
                            new { name },
                            splitOn: "OrderID")
                            .Distinct()
                            .ToList();
                }
            }

            stop.Stop();
            return Ok(stop.ElapsedMilliseconds);
        }


    }
}