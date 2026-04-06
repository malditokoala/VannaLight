using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

public sealed class NorthwindSalesDomainPackAdapter
{
    public DomainPackDefinition Build(string domain, string? connectionName = null)
    {
        var normalizedDomain = string.IsNullOrWhiteSpace(domain) ? "northwind-sales" : domain.Trim();

        return new DomainPackDefinition
        {
            Key = $"{normalizedDomain}:northwind-sales",
            DisplayName = $"Northwind Sales Pack · {normalizedDomain}",
            Domain = normalizedDomain,
            ConnectionName = string.IsNullOrWhiteSpace(connectionName) ? "NorthwindDb" : connectionName.Trim(),
            CalendarProfileKey = "standard-calendar",
            Description = "Pack semántico para forecast de ventas y demanda en Northwind.",
            Metrics =
            [
                new MetricDefinition
                {
                    Key = "net_sales",
                    DisplayName = "Net Sales",
                    Description = "Venta neta calculada desde detalle de pedidos.",
                    TimeColumn = "OrderDate",
                    SqlExpression = "SUM(od.UnitPrice * od.Quantity * (1 - od.Discount))",
                    BaseObject = "dbo.Orders o JOIN dbo.OrderDetails od ON o.OrderID = od.OrderID",
                    DefaultAggregation = "sum",
                    AllowedDimensions = ["product", "category", "customer", "ship_country"]
                },
                new MetricDefinition
                {
                    Key = "units_sold",
                    DisplayName = "Units Sold",
                    Description = "Unidades vendidas agregadas por fecha.",
                    TimeColumn = "OrderDate",
                    SqlExpression = "SUM(od.Quantity)",
                    BaseObject = "dbo.Orders o JOIN dbo.OrderDetails od ON o.OrderID = od.OrderID",
                    DefaultAggregation = "sum",
                    AllowedDimensions = ["product", "category", "customer", "ship_country"]
                },
                new MetricDefinition
                {
                    Key = "order_count",
                    DisplayName = "Order Count",
                    Description = "Conteo de órdenes en el periodo.",
                    TimeColumn = "OrderDate",
                    SqlExpression = "COUNT(DISTINCT o.OrderID)",
                    BaseObject = "dbo.Orders o",
                    DefaultAggregation = "count",
                    AllowedDimensions = ["customer", "employee", "ship_country"]
                }
            ],
            Dimensions =
            [
                new DimensionDefinition
                {
                    Key = "product",
                    DisplayName = "Product",
                    Description = "Producto vendido.",
                    SqlExpression = "p.ProductName"
                },
                new DimensionDefinition
                {
                    Key = "category",
                    DisplayName = "Category",
                    Description = "Categoría del producto.",
                    SqlExpression = "c.CategoryName"
                },
                new DimensionDefinition
                {
                    Key = "customer",
                    DisplayName = "Customer",
                    Description = "Cliente de la orden.",
                    SqlExpression = "cu.CustomerName"
                },
                new DimensionDefinition
                {
                    Key = "ship_country",
                    DisplayName = "Ship Country",
                    Description = "País de envío.",
                    SqlExpression = "o.ShipCountry"
                },
                new DimensionDefinition
                {
                    Key = "employee",
                    DisplayName = "Employee",
                    Description = "Empleado responsable de la orden.",
                    SqlExpression = "e.FirstName + ' ' + e.LastName"
                }
            ]
        };
    }
}
