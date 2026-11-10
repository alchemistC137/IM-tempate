namespace MemByte.Domain.Orders;

/// <summary>
/// Pedido de un cliente de la plantilla. Es la unidad de aislamiento:
/// toda consulta de pedidos debe filtrar primero por <see cref="ClientId"/>
/// para impedir que un usuario de un cliente vea pedidos de otro.
/// <para>
/// Vista genérica de plantilla: cada cliente de MemByte-IM ordena artículos
/// de su propio catálogo (<c>MemByte.Domain.Catalog</c>). En el caso de
/// MemByte, sus clientes ordenan soluciones digitales y contratan servicios
/// de mantenimiento; esto es solo un ejemplo del uso futuro del módulo.
/// </para>
/// </summary>
public class Order
{
	public const int NotesMaxLength = 1000;
	public const int OrderNumberMaxLength = 32;

	private readonly List<OrderLine> _lines = [];

	public Guid Id { get; private set; }

	/// <summary>Cliente de la plantilla propietario del pedido.</summary>
	public Guid ClientId { get; private set; }

	/// <summary>Número de pedido legible, único dentro del cliente.</summary>
	public string OrderNumber { get; private set; } = string.Empty;

	/// <summary>Usuario que creó el pedido (se resuelve siempre en servidor).</summary>
	public Guid CreatedByUserId { get; private set; }

	public OrderStatus Status { get; private set; } = OrderStatus.Draft;

	public OrderPaymentStatus PaymentStatus { get; private set; } = OrderPaymentStatus.Pending;

	public string Notes { get; private set; } = string.Empty;

	public DateTimeOffset CreatedAt { get; private set; }

	public DateTimeOffset UpdatedAt { get; private set; }

	public DateTimeOffset? ConfirmedAt { get; private set; }

	public DateTimeOffset? CompletedAt { get; private set; }

	public DateTimeOffset? CancelledAt { get; private set; }

	public IReadOnlyCollection<OrderLine> Lines => _lines;

	public decimal Total => _lines.Sum(l => l.LineTotal);

	private Order()
	{
		// Requerido por EF Core.
	}

	public Order(Guid clientId, string orderNumber, Guid createdByUserId, string? notes = null)
	{
		if (clientId == Guid.Empty)
			throw new ArgumentException("El pedido debe pertenecer a un cliente.", nameof(clientId));

		if (createdByUserId == Guid.Empty)
			throw new ArgumentException("El autor del pedido no es válido.", nameof(createdByUserId));

		Id = Guid.NewGuid();
		ClientId = clientId;
		CreatedByUserId = createdByUserId;
		CreatedAt = DateTimeOffset.UtcNow;
		UpdatedAt = CreatedAt;
		SetOrderNumber(orderNumber);
		SetNotes(notes);
	}

	public void UpdateNotes(string? notes)
	{
		SetNotes(notes);
		Touch();
	}

	/// <summary>
	/// Añade una línea al pedido. Sólo se permite mientras el pedido está en
	/// borrador, para no alterar un pedido ya confirmado.
	/// </summary>
	public OrderLine AddLine(Guid catalogItemId, string itemNameSnapshot, decimal unitPriceSnapshot, string priceCurrencyCode, int quantity)
	{
		EnsureDraft();

		var line = new OrderLine(Id, catalogItemId, itemNameSnapshot, unitPriceSnapshot, priceCurrencyCode, quantity);
		_lines.Add(line);
		Touch();
		return line;
	}

	public void RemoveLine(Guid lineId)
	{
		EnsureDraft();

		var line = _lines.FirstOrDefault(l => l.Id == lineId)
			?? throw new InvalidOperationException("La línea indicada no existe en este pedido.");

		_lines.Remove(line);
		Touch();
	}

	public void UpdateLineQuantity(Guid lineId, int quantity)
	{
		EnsureDraft();

		var line = _lines.FirstOrDefault(l => l.Id == lineId)
			?? throw new InvalidOperationException("La línea indicada no existe en este pedido.");

		line.UpdateQuantity(quantity);
		Touch();
	}

	/// <summary>
	/// Confirma el pedido: pasa de borrador a confirmado. Requiere al menos
	/// una línea, para no confirmar pedidos vacíos.
	/// </summary>
	public void Confirm()
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Sólo se puede confirmar un pedido en borrador.");

		if (_lines.Count == 0)
			throw new InvalidOperationException("El pedido no tiene líneas; no se puede confirmar.");

		Status = OrderStatus.Confirmed;
		ConfirmedAt = DateTimeOffset.UtcNow;
		Touch();
	}

	public void StartProgress()
	{
		if (Status != OrderStatus.Confirmed)
			throw new InvalidOperationException("Sólo se puede iniciar un pedido ya confirmado.");

		Status = OrderStatus.InProgress;
		Touch();
	}

	public void Complete()
	{
		if (Status is not (OrderStatus.Confirmed or OrderStatus.InProgress))
			throw new InvalidOperationException("Sólo se puede completar un pedido confirmado o en curso.");

		Status = OrderStatus.Completed;
		CompletedAt = DateTimeOffset.UtcNow;
		Touch();
	}

	public void Cancel()
	{
		if (Status is OrderStatus.Completed or OrderStatus.Cancelled)
			throw new InvalidOperationException("Un pedido completado o ya cancelado no puede cancelarse.");

		Status = OrderStatus.Cancelled;
		CancelledAt = DateTimeOffset.UtcNow;
		Touch();
	}

	public void MarkAsPaid()
	{
		if (Status == OrderStatus.Cancelled)
			throw new InvalidOperationException("Un pedido cancelado no puede marcarse como pagado.");

		PaymentStatus = OrderPaymentStatus.Paid;
		Touch();
	}

	private void EnsureDraft()
	{
		if (Status != OrderStatus.Draft)
			throw new InvalidOperationException("Sólo se pueden modificar las líneas de un pedido en borrador.");
	}

	private void SetOrderNumber(string orderNumber)
	{
		if (string.IsNullOrWhiteSpace(orderNumber))
			throw new ArgumentException("El número de pedido no puede estar vacío.", nameof(orderNumber));

		orderNumber = orderNumber.Trim();
		OrderNumber = orderNumber.Length > OrderNumberMaxLength ? orderNumber[..OrderNumberMaxLength] : orderNumber;
	}

	private void SetNotes(string? notes)
	{
		notes = (notes ?? string.Empty).Trim();
		Notes = notes.Length > NotesMaxLength ? notes[..NotesMaxLength] : notes;
	}

	private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
