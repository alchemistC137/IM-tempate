using MemByte.Application.Orders;
using MemByte.Domain.Catalog;
using MemByte.Domain.Orders;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Orders;

/// <summary>
/// Implementación de <see cref="IOrderService"/> sobre
/// <see cref="MemByteDbContext"/>, siguiendo el mismo estilo que
/// <c>CatalogService</c>/<c>MessagingService</c>: sin repositorio genérico
/// adicional. Todas las consultas y mutaciones acotadas a un cliente filtran
/// siempre por <c>clientId</c>, que es la barrera de aislamiento entre
/// pedidos de distintos clientes de la plantilla.
/// </summary>
public class OrderService : IOrderService
{
	private readonly MemByteDbContext _dbContext;

	public OrderService(MemByteDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersAsync(Guid clientId, OrderStatus? status = null, CancellationToken cancellationToken = default)
	{
		var query = _dbContext.Orders.AsNoTracking().Where(o => o.ClientId == clientId);
		if (status is OrderStatus s)
			query = query.Where(o => o.Status == s);

		return await query
			.OrderByDescending(o => o.CreatedAt)
			.Select(o => ToSummaryDto(o))
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<OrderSummaryDto>> GetAllOrdersAsync(OrderStatus? status = null, CancellationToken cancellationToken = default)
	{
		var query = _dbContext.Orders.AsNoTracking().AsQueryable();
		if (status is OrderStatus s)
			query = query.Where(o => o.Status == s);

		return await query
			.OrderByDescending(o => o.CreatedAt)
			.Select(o => ToSummaryDto(o))
			.ToListAsync(cancellationToken);
	}

	public async Task<OrderDto?> GetOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default)
	{
		var order = await FindOrderAsync(clientId, orderId, tracking: false, cancellationToken);
		return order is null ? null : ToDto(order);
	}

	public async Task<OrderOperationResult<OrderDto>> CreateOrderAsync(Guid clientId, Guid createdByUserId, CreateOrderRequest request, CancellationToken cancellationToken = default)
	{
		Order order;
		try
		{
			var orderNumber = await GenerateOrderNumberAsync(clientId, cancellationToken);
			order = new Order(clientId, orderNumber, createdByUserId, request.Notes);
		}
		catch (ArgumentException ex)
		{
			return OrderOperationResult<OrderDto>.Failure(ex.Message);
		}

		_dbContext.Orders.Add(order);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return OrderOperationResult<OrderDto>.Success(ToDto(order));
	}

	public async Task<OrderOperationResult<OrderDto>> UpdateNotesAsync(Guid clientId, Guid orderId, UpdateOrderNotesRequest request, CancellationToken cancellationToken = default)
	{
		var order = await FindOrderAsync(clientId, orderId, tracking: true, cancellationToken);
		if (order is null)
			return OrderOperationResult<OrderDto>.Failure("El pedido no existe.");

		order.UpdateNotes(request.Notes);

		await _dbContext.SaveChangesAsync(cancellationToken);
		return OrderOperationResult<OrderDto>.Success(ToDto(order));
	}

	public async Task<OrderOperationResult<OrderDto>> AddLineAsync(Guid clientId, Guid orderId, AddOrderLineRequest request, CancellationToken cancellationToken = default)
	{
		var order = await FindOrderAsync(clientId, orderId, tracking: true, cancellationToken);
		if (order is null)
			return OrderOperationResult<OrderDto>.Failure("El pedido no existe.");

		var catalogItem = await _dbContext.Set<CatalogItem>()
			.AsNoTracking()
			.FirstOrDefaultAsync(i => i.Id == request.CatalogItemId && i.ClientId == clientId, cancellationToken);
		if (catalogItem is null)
			return OrderOperationResult<OrderDto>.Failure("El artículo del catálogo indicado no existe.");

		try
		{
			order.AddLine(catalogItem.Id, catalogItem.Name, catalogItem.Price.Amount, catalogItem.Price.CurrencyCode, request.Quantity);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			return OrderOperationResult<OrderDto>.Failure(ex.Message);
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		return OrderOperationResult<OrderDto>.Success(ToDto(order));
	}

	public async Task<OrderOperationResult<OrderDto>> RemoveLineAsync(Guid clientId, Guid orderId, Guid lineId, CancellationToken cancellationToken = default)
	{
		var order = await FindOrderAsync(clientId, orderId, tracking: true, cancellationToken);
		if (order is null)
			return OrderOperationResult<OrderDto>.Failure("El pedido no existe.");

		try
		{
			order.RemoveLine(lineId);
		}
		catch (InvalidOperationException ex)
		{
			return OrderOperationResult<OrderDto>.Failure(ex.Message);
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		return OrderOperationResult<OrderDto>.Success(ToDto(order));
	}

	public async Task<OrderOperationResult<OrderDto>> UpdateLineQuantityAsync(Guid clientId, Guid orderId, Guid lineId, UpdateOrderLineQuantityRequest request, CancellationToken cancellationToken = default)
	{
		var order = await FindOrderAsync(clientId, orderId, tracking: true, cancellationToken);
		if (order is null)
			return OrderOperationResult<OrderDto>.Failure("El pedido no existe.");

		try
		{
			order.UpdateLineQuantity(lineId, request.Quantity);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			return OrderOperationResult<OrderDto>.Failure(ex.Message);
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		return OrderOperationResult<OrderDto>.Success(ToDto(order));
	}

	public async Task<OrderOperationResult<OrderDto>> ConfirmOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default)
		=> await ApplyTransitionAsync(clientId, orderId, order => order.Confirm(), cancellationToken);

	public async Task<OrderOperationResult<OrderDto>> StartProgressAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default)
		=> await ApplyTransitionAsync(clientId, orderId, order => order.StartProgress(), cancellationToken);

	public async Task<OrderOperationResult<OrderDto>> CompleteOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default)
		=> await ApplyTransitionAsync(clientId, orderId, order => order.Complete(), cancellationToken);

	public async Task<OrderOperationResult<OrderDto>> CancelOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default)
		=> await ApplyTransitionAsync(clientId, orderId, order => order.Cancel(), cancellationToken);

	public async Task<OrderOperationResult<OrderDto>> MarkAsPaidAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default)
		=> await ApplyTransitionAsync(clientId, orderId, order => order.MarkAsPaid(), cancellationToken);

	public async Task<OrderOperationResult<bool>> DeleteOrderAsync(Guid clientId, Guid orderId, CancellationToken cancellationToken = default)
	{
		var order = await FindOrderAsync(clientId, orderId, tracking: true, cancellationToken);
		if (order is null)
			return OrderOperationResult<bool>.Failure("El pedido no existe.");

		if (order.Status != OrderStatus.Draft)
			return OrderOperationResult<bool>.Failure("Sólo se puede eliminar un pedido en borrador.");

		_dbContext.Orders.Remove(order);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return OrderOperationResult<bool>.Success(true);
	}

	private async Task<OrderOperationResult<OrderDto>> ApplyTransitionAsync(Guid clientId, Guid orderId, Action<Order> transition, CancellationToken cancellationToken)
	{
		var order = await FindOrderAsync(clientId, orderId, tracking: true, cancellationToken);
		if (order is null)
			return OrderOperationResult<OrderDto>.Failure("El pedido no existe.");

		try
		{
			transition(order);
		}
		catch (InvalidOperationException ex)
		{
			return OrderOperationResult<OrderDto>.Failure(ex.Message);
		}

		await _dbContext.SaveChangesAsync(cancellationToken);
		return OrderOperationResult<OrderDto>.Success(ToDto(order));
	}

	private async Task<Order?> FindOrderAsync(Guid clientId, Guid orderId, bool tracking, CancellationToken cancellationToken)
	{
		var query = _dbContext.Orders.Include(o => o.Lines).Where(o => o.ClientId == clientId && o.Id == orderId);
		if (!tracking)
			query = query.AsNoTracking();

		return await query.FirstOrDefaultAsync(cancellationToken);
	}

	private async Task<string> GenerateOrderNumberAsync(Guid clientId, CancellationToken cancellationToken)
	{
		var count = await _dbContext.Orders.AsNoTracking().CountAsync(o => o.ClientId == clientId, cancellationToken);
		return $"PED-{count + 1:D6}";
	}

	private static OrderSummaryDto ToSummaryDto(Order order) => new(
		order.Id,
		order.ClientId,
		order.OrderNumber,
		order.Status,
		order.PaymentStatus,
		order.Total,
		order.CreatedAt,
		order.UpdatedAt);

	private static OrderDto ToDto(Order order) => new(
		order.Id,
		order.ClientId,
		order.OrderNumber,
		order.CreatedByUserId,
		order.Status,
		order.PaymentStatus,
		order.Notes,
		order.Total,
		order.CreatedAt,
		order.UpdatedAt,
		order.ConfirmedAt,
		order.CompletedAt,
		order.CancelledAt,
		order.Lines.Select(ToLineDto).ToList());

	private static OrderLineDto ToLineDto(OrderLine line) => new(
		line.Id,
		line.OrderId,
		line.CatalogItemId,
		line.ItemNameSnapshot,
		line.UnitPriceSnapshot,
		line.PriceCurrencyCode,
		line.Quantity,
		line.LineTotal);
}
