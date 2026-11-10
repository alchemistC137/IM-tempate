using MemByte.Domain.Catalog;
using MemByte.Domain.Clients;
using MemByte.Domain.Employees;
using MemByte.Domain.Messaging;
using MemByte.Domain.Orders;
using MemByte.Infrastructure.Audit;
using MemByte.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Persistence;

public class MemByteDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
	public MemByteDbContext(DbContextOptions<MemByteDbContext> options)
		: base(options)
	{
	}

	public DbSet<Employee> Employees => Set<Employee>();

	public DbSet<Client> Clients => Set<Client>();

	public DbSet<AuthAuditEntry> AuthAuditEntries => Set<AuthAuditEntry>();

	public DbSet<UserSession> UserSessions => Set<UserSession>();

	public DbSet<Conversation> Conversations => Set<Conversation>();

	public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

	public DbSet<Message> Messages => Set<Message>();

	public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

	public DbSet<MessagingAuditEntry> MessagingAuditEntries => Set<MessagingAuditEntry>();

	public DbSet<CatalogCategory> CatalogCategories => Set<CatalogCategory>();

	public DbSet<Product> Products => Set<Product>();

	public DbSet<Service> Services => Set<Service>();

	public DbSet<Order> Orders => Set<Order>();

	public DbSet<OrderLine> OrderLines => Set<OrderLine>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<Employee>(entity =>
		{
			entity.ToTable("Employees");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
			entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
			entity.Property(e => e.Department).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Role).IsRequired().HasMaxLength(64).HasDefaultValue(EmployeeRoles.Employee);
			entity.Property(e => e.IsActive).HasDefaultValue(true);
			entity.HasIndex(e => e.Email).IsUnique();
		});

		modelBuilder.Entity<Client>(entity =>
		{
			entity.ToTable("Clients");
			entity.HasKey(c => c.Id);
			entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
			entity.Property(c => c.IsActive).HasDefaultValue(true);
			entity.HasIndex(c => c.Name);
		});

		modelBuilder.Entity<ApplicationUser>(entity =>
		{
			entity.ToTable("Users");
			entity.Property(u => u.Timezone).IsRequired().HasMaxLength(64);
			entity.Property(u => u.DisplayName).HasMaxLength(200);
			entity.HasIndex(u => u.EmployeeId);

			// Restrict (no cascade): un Client no puede eliminarse mientras
			// tenga usuarios vinculados; primero hay que desvincularlos o
			// reasignarlos desde el módulo de Gestión de Usuarios.
			entity.HasOne<Client>()
				.WithMany()
				.HasForeignKey(u => u.ClientId)
				.OnDelete(DeleteBehavior.Restrict);
			entity.HasIndex(u => u.ClientId);
		});

		modelBuilder.Entity<ApplicationRole>(entity =>
		{
			entity.ToTable("Roles");
		});

		modelBuilder.Entity<AuthAuditEntry>(entity =>
		{
			entity.ToTable("AuthAuditEntries");
			entity.HasKey(a => a.Id);
			entity.Property(a => a.EventType).IsRequired().HasMaxLength(64);
			entity.Property(a => a.FailureReason).HasMaxLength(256);
			entity.Property(a => a.IpAddress).HasMaxLength(64);
			entity.Property(a => a.UserAgent).HasMaxLength(512);
			entity.HasIndex(a => a.UserId);
			entity.HasIndex(a => a.OccurredAt);
		});

		modelBuilder.Entity<UserSession>(entity =>
		{
			entity.ToTable("UserSessions");
			entity.HasKey(s => s.Id);
			entity.Property(s => s.SessionToken).IsRequired().HasMaxLength(128);
			entity.Property(s => s.IpAddress).HasMaxLength(64);
			entity.Property(s => s.UserAgent).HasMaxLength(512);
			entity.HasIndex(s => s.UserId);
			entity.HasIndex(s => s.SessionToken).IsUnique();
		});

		modelBuilder.Entity<PasswordHistoryEntry>(entity =>
		{
			entity.ToTable("PasswordHistoryEntries");
			entity.HasKey(p => p.Id);
			entity.Property(p => p.PasswordHash).IsRequired();
			entity.HasIndex(p => new { p.UserId, p.CreatedAt });
		});

		ConfigureMessaging(modelBuilder);
		ConfigureCatalog(modelBuilder);
		ConfigureOrders(modelBuilder);
	}

	/// <summary>
	/// Configuración del módulo de mensajería interna. El aislamiento por
	/// cliente se apoya en <c>Conversation.ClientId</c> (nulo = conversación
	/// interna de MemByte) y en los índices que fuerzan a que toda consulta de
	/// bandeja pase por cliente + fecha del último mensaje.
	/// </summary>
	private static void ConfigureMessaging(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Conversation>(entity =>
		{
			entity.ToTable("Conversations");
			entity.HasKey(c => c.Id);
			// El identificador lo asigna el dominio; si EF lo considerase
			// generado por la base de datos, las entidades nuevas se tomarían
			// por modificadas al adjuntarse al agregado.
			entity.Property(c => c.Id).ValueGeneratedNever();
			entity.Property(c => c.Subject).IsRequired().HasMaxLength(Conversation.SubjectMaxLength);
			entity.Property(c => c.LastMessagePreview).HasMaxLength(Conversation.PreviewMaxLength);
			entity.Property(c => c.Status).HasConversion<int>();
			entity.Property(c => c.Priority).HasConversion<int>();

			// Restrict: el histórico de mensajería no debe desaparecer en cascada
			// al eliminar un cliente; primero hay que aplicar la política de
			// retención/purga explícita.
			entity.HasOne<Client>()
				.WithMany()
				.HasForeignKey(c => c.ClientId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(c => new { c.ClientId, c.LastMessageAt });
			entity.HasIndex(c => new { c.ClientId, c.Status });
			entity.HasIndex(c => c.CreatedByUserId);

			entity.Navigation(c => c.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
			entity.Navigation(c => c.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
		});

		modelBuilder.Entity<ConversationParticipant>(entity =>
		{
			entity.ToTable("ConversationParticipants");
			entity.HasKey(p => p.Id);
			entity.Property(p => p.Id).ValueGeneratedNever();
			entity.Property(p => p.Role).HasConversion<int>();

			entity.HasOne<Conversation>()
				.WithMany(c => c.Participants)
				.HasForeignKey(p => p.ConversationId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne<ApplicationUser>()
				.WithMany()
				.HasForeignKey(p => p.UserId)
				.OnDelete(DeleteBehavior.Restrict);

			// Un usuario no puede estar dos veces en la misma conversación: evita
			// duplicar permisos de lectura y contadores de no leídos.
			entity.HasIndex(p => new { p.ConversationId, p.UserId }).IsUnique();
			entity.HasIndex(p => new { p.UserId, p.UnreadCount });
		});

		modelBuilder.Entity<Message>(entity =>
		{
			entity.ToTable("Messages");
			entity.HasKey(m => m.Id);
			entity.Property(m => m.Id).ValueGeneratedNever();
			entity.Property(m => m.Body).IsRequired().HasMaxLength(Message.BodyMaxLength);

			entity.HasOne<Conversation>()
				.WithMany(c => c.Messages)
				.HasForeignKey(m => m.ConversationId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne<ApplicationUser>()
				.WithMany()
				.HasForeignKey(m => m.SenderUserId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(m => new { m.ConversationId, m.CreatedAt });

			entity.Navigation(m => m.Attachments).UsePropertyAccessMode(PropertyAccessMode.Field);
		});

		modelBuilder.Entity<MessageAttachment>(entity =>
		{
			entity.ToTable("MessageAttachments");
			entity.HasKey(a => a.Id);
			entity.Property(a => a.Id).ValueGeneratedNever();
			entity.Property(a => a.FileName).IsRequired().HasMaxLength(MessageAttachment.FileNameMaxLength);
			entity.Property(a => a.ContentType).IsRequired().HasMaxLength(128);
			entity.Property(a => a.StorageReference).IsRequired().HasMaxLength(512);

			entity.HasOne<Message>()
				.WithMany(m => m.Attachments)
				.HasForeignKey(a => a.MessageId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasIndex(a => a.MessageId);
		});

		modelBuilder.Entity<MessagingAuditEntry>(entity =>
		{
			entity.ToTable("MessagingAuditEntries");
			entity.HasKey(a => a.Id);
			entity.Property(a => a.Id).ValueGeneratedNever();
			entity.Property(a => a.EventType).IsRequired().HasMaxLength(64);
			entity.Property(a => a.Details).HasMaxLength(512);
			entity.HasIndex(a => a.ConversationId);
			entity.HasIndex(a => a.OccurredAt);
			entity.HasIndex(a => new { a.UserId, a.OccurredAt });
		});
	}

	/// <summary>
	/// Configuración del módulo de catálogo de productos y servicios. Cada
	/// cliente de la plantilla tiene su propio catálogo, aislado mediante
	/// <c>ClientId</c> (obligatorio, sin catálogo global). <c>Product</c> y
	/// <c>Service</c> comparten tabla (TPH) porque heredan de
	/// <c>CatalogItem</c> y ambos son, conceptualmente, artículos del mismo
	/// catálogo.
	/// </summary>
	private static void ConfigureCatalog(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<CatalogCategory>(entity =>
		{
			entity.ToTable("CatalogCategories");
			entity.HasKey(c => c.Id);
			entity.Property(c => c.Id).ValueGeneratedNever();
			entity.Property(c => c.Name).IsRequired().HasMaxLength(CatalogCategory.NameMaxLength);

			entity.HasOne<Client>()
				.WithMany()
				.HasForeignKey(c => c.ClientId)
				.OnDelete(DeleteBehavior.Restrict);

			// Restrict: no se puede borrar una categoría padre en cascada;
			// primero hay que mover o eliminar sus subcategorías (lo aplica
			// CatalogService antes de llegar aquí).
			entity.HasOne<CatalogCategory>()
				.WithMany()
				.HasForeignKey(c => c.ParentCategoryId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(c => new { c.ClientId, c.Name });
			entity.HasIndex(c => c.ParentCategoryId);
		});

		modelBuilder.Entity<CatalogItem>(entity =>
		{
			entity.ToTable("CatalogItems");
			entity.HasKey(i => i.Id);
			entity.Property(i => i.Id).ValueGeneratedNever();
			entity.Property(i => i.Name).IsRequired().HasMaxLength(CatalogItem.NameMaxLength);
			entity.Property(i => i.Description).HasMaxLength(CatalogItem.DescriptionMaxLength);
			entity.HasDiscriminator<string>("ItemType")
				.HasValue<Product>("Product")
				.HasValue<Service>("Service");

			entity.OwnsOne(i => i.Price, price =>
			{
				price.Property(p => p.Amount).HasColumnName("PriceAmount").HasColumnType("decimal(18,2)").IsRequired();
				price.Property(p => p.CurrencyCode).HasColumnName("PriceCurrencyCode").HasMaxLength(Price.CurrencyCodeLength).IsRequired();
			});
			entity.Navigation(i => i.Price).IsRequired();

			entity.HasOne<Client>()
				.WithMany()
				.HasForeignKey(i => i.ClientId)
				.OnDelete(DeleteBehavior.Restrict);

			// Restrict: no se puede eliminar una categoría que tenga artículos
			// asociados (lo aplica CatalogService antes de llegar aquí).
			entity.HasOne<CatalogCategory>()
				.WithMany()
				.HasForeignKey(i => i.CategoryId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(i => new { i.ClientId, i.Name });
			entity.HasIndex(i => i.CategoryId);
		});

		modelBuilder.Entity<Product>(entity =>
		{
			entity.Property(p => p.Sku).HasMaxLength(Product.SkuMaxLength);
		});

		modelBuilder.Entity<Service>();
	}

	/// <summary>
	/// Configuración del módulo de pedidos. El aislamiento por cliente se
	/// apoya en <c>Order.ClientId</c>; las líneas del pedido se conservan
	/// con una "foto" del artículo del catálogo, por lo que la relación con
	/// <c>CatalogItem</c> es informativa (Restrict) y no cascada.
	/// </summary>
	private static void ConfigureOrders(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Order>(entity =>
		{
			entity.ToTable("Orders");
			entity.HasKey(o => o.Id);
			entity.Property(o => o.Id).ValueGeneratedNever();
			entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(Order.OrderNumberMaxLength);
			entity.Property(o => o.Notes).HasMaxLength(Order.NotesMaxLength);
			entity.Property(o => o.Status).HasConversion<int>();
			entity.Property(o => o.PaymentStatus).HasConversion<int>();
			entity.Ignore(o => o.Total);

			entity.HasOne<Client>()
				.WithMany()
				.HasForeignKey(o => o.ClientId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasOne<ApplicationUser>()
				.WithMany()
				.HasForeignKey(o => o.CreatedByUserId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(o => new { o.ClientId, o.OrderNumber }).IsUnique();
			entity.HasIndex(o => new { o.ClientId, o.Status });

			entity.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
		});

		modelBuilder.Entity<OrderLine>(entity =>
		{
			entity.ToTable("OrderLines");
			entity.HasKey(l => l.Id);
			entity.Property(l => l.Id).ValueGeneratedNever();
			entity.Property(l => l.ItemNameSnapshot).IsRequired().HasMaxLength(OrderLine.ItemNameSnapshotMaxLength);
			entity.Property(l => l.UnitPriceSnapshot).HasColumnType("decimal(18,2)");
			entity.Property(l => l.PriceCurrencyCode).IsRequired().HasMaxLength(Price.CurrencyCodeLength);
			entity.Ignore(l => l.LineTotal);

			entity.HasOne<Order>()
				.WithMany(o => o.Lines)
				.HasForeignKey(l => l.OrderId)
				.OnDelete(DeleteBehavior.Cascade);

			// Restrict: no se puede borrar un artículo del catálogo que
			// tenga líneas de pedido asociadas; el histórico conserva su
			// nombre y precio en el momento del pedido.
			entity.HasOne<CatalogItem>()
				.WithMany()
				.HasForeignKey(l => l.CatalogItemId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasIndex(l => l.OrderId);
			entity.HasIndex(l => l.CatalogItemId);
		});
	}
}
