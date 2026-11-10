using MemByte.Domain.Orders;

namespace MemByte.Domain.Tests.Orders;

public class OrderTests
{
	private static Guid ClientId => Guid.NewGuid();

	private static Guid UserId => Guid.NewGuid();

	private static Order CreateDraftOrder() => new(ClientId, "PED-000001", UserId);

	[Fact]
	public void Constructor_WithEmptyClientId_Throws()
	{
		Assert.Throws<ArgumentException>(() => new Order(Guid.Empty, "PED-000001", UserId));
	}

	[Fact]
	public void Constructor_WithEmptyCreatedByUserId_Throws()
	{
		Assert.Throws<ArgumentException>(() => new Order(ClientId, "PED-000001", Guid.Empty));
	}

	[Fact]
	public void Constructor_SetsDraftStatusAndPendingPayment()
	{
		var order = CreateDraftOrder();

		Assert.Equal(OrderStatus.Draft, order.Status);
		Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);
		Assert.Empty(order.Lines);
	}

	[Fact]
	public void AddLine_AccumulatesTotal()
	{
		var order = CreateDraftOrder();

		order.AddLine(Guid.NewGuid(), "Mantenimiento mensual", 50m, "EUR", 2);
		order.AddLine(Guid.NewGuid(), "Licencia anual", 100m, "EUR", 1);

		Assert.Equal(200m, order.Total);
	}

	[Fact]
	public void AddLine_WithZeroQuantity_Throws()
	{
		var order = CreateDraftOrder();

		Assert.Throws<ArgumentException>(() => order.AddLine(Guid.NewGuid(), "Servicio", 10m, "EUR", 0));
	}

	[Fact]
	public void AddLine_AfterConfirm_Throws()
	{
		var order = CreateDraftOrder();
		order.AddLine(Guid.NewGuid(), "Servicio", 10m, "EUR", 1);
		order.Confirm();

		Assert.Throws<InvalidOperationException>(() => order.AddLine(Guid.NewGuid(), "Otro servicio", 5m, "EUR", 1));
	}

	[Fact]
	public void RemoveLine_WithUnknownId_Throws()
	{
		var order = CreateDraftOrder();

		Assert.Throws<InvalidOperationException>(() => order.RemoveLine(Guid.NewGuid()));
	}

	[Fact]
	public void UpdateLineQuantity_ChangesLineTotal()
	{
		var order = CreateDraftOrder();
		var line = order.AddLine(Guid.NewGuid(), "Servicio", 10m, "EUR", 1);

		order.UpdateLineQuantity(line.Id, 3);

		Assert.Equal(30m, order.Total);
	}

	[Fact]
	public void Confirm_WithoutLines_Throws()
	{
		var order = CreateDraftOrder();

		Assert.Throws<InvalidOperationException>(() => order.Confirm());
	}

	[Fact]
	public void Confirm_WithLines_SetsConfirmedStatusAndTimestamp()
	{
		var order = CreateDraftOrder();
		order.AddLine(Guid.NewGuid(), "Servicio", 10m, "EUR", 1);

		order.Confirm();

		Assert.Equal(OrderStatus.Confirmed, order.Status);
		Assert.NotNull(order.ConfirmedAt);
	}

	[Fact]
	public void Confirm_Twice_Throws()
	{
		var order = CreateDraftOrder();
		order.AddLine(Guid.NewGuid(), "Servicio", 10m, "EUR", 1);
		order.Confirm();

		Assert.Throws<InvalidOperationException>(() => order.Confirm());
	}

	[Fact]
	public void StartProgress_WithoutConfirm_Throws()
	{
		var order = CreateDraftOrder();

		Assert.Throws<InvalidOperationException>(() => order.StartProgress());
	}

	[Fact]
	public void Complete_FromConfirmed_SetsCompletedStatusAndTimestamp()
	{
		var order = CreateDraftOrder();
		order.AddLine(Guid.NewGuid(), "Servicio", 10m, "EUR", 1);
		order.Confirm();

		order.Complete();

		Assert.Equal(OrderStatus.Completed, order.Status);
		Assert.NotNull(order.CompletedAt);
	}

	[Fact]
	public void Complete_FromDraft_Throws()
	{
		var order = CreateDraftOrder();

		Assert.Throws<InvalidOperationException>(() => order.Complete());
	}

	[Fact]
	public void Cancel_FromCompleted_Throws()
	{
		var order = CreateDraftOrder();
		order.AddLine(Guid.NewGuid(), "Servicio", 10m, "EUR", 1);
		order.Confirm();
		order.Complete();

		Assert.Throws<InvalidOperationException>(() => order.Cancel());
	}

	[Fact]
	public void Cancel_FromDraft_SetsCancelledStatusAndTimestamp()
	{
		var order = CreateDraftOrder();

		order.Cancel();

		Assert.Equal(OrderStatus.Cancelled, order.Status);
		Assert.NotNull(order.CancelledAt);
	}

	[Fact]
	public void MarkAsPaid_OnCancelledOrder_Throws()
	{
		var order = CreateDraftOrder();
		order.Cancel();

		Assert.Throws<InvalidOperationException>(() => order.MarkAsPaid());
	}

	[Fact]
	public void MarkAsPaid_SetsPaymentStatusToPaid()
	{
		var order = CreateDraftOrder();

		order.MarkAsPaid();

		Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
	}
}
