using MemByte.Application.Clients;
using MemByte.Domain.Clients;
using MemByte.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MemByte.Infrastructure.Clients;

/// <summary>
/// Implementación de <see cref="IClientService"/> sobre <see cref="MemByteDbContext"/>.
/// Sigue el mismo estilo que <c>EmployeeService</c>: sin repositorio genérico
/// adicional, el DbContext se usa directamente desde el servicio de
/// aplicación/infraestructura.
/// </summary>
public class ClientService : IClientService
{
	private readonly MemByteDbContext _dbContext;

	public ClientService(MemByteDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await _dbContext.Clients
			.AsNoTracking()
			.OrderBy(c => c.Name)
			.Select(c => ToDto(c))
			.ToListAsync(cancellationToken);
	}

	public async Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var client = await _dbContext.Clients
			.AsNoTracking()
			.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

		return client is null ? null : ToDto(client);
	}

	public async Task<ClientOperationResult> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default)
	{
		if (await NameInUseAsync(request.Name, excludeId: null, cancellationToken))
			return ClientOperationResult.Failure("Ya existe un cliente con ese nombre.");

		Client client;
		try
		{
			client = new Client(request.Name, request.IsActive);
		}
		catch (ArgumentException ex)
		{
			return ClientOperationResult.Failure(ex.Message);
		}

		_dbContext.Clients.Add(client);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return ClientOperationResult.Success(ToDto(client));
	}

	public async Task<ClientOperationResult> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken = default)
	{
		var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
		if (client is null)
			return ClientOperationResult.Failure("El cliente no existe.");

		if (await NameInUseAsync(request.Name, excludeId: id, cancellationToken))
			return ClientOperationResult.Failure("Ya existe un cliente con ese nombre.");

		try
		{
			client.UpdateDetails(request.Name);
		}
		catch (ArgumentException ex)
		{
			return ClientOperationResult.Failure(ex.Message);
		}

		if (request.IsActive)
			client.Activate();
		else
			client.Deactivate();

		await _dbContext.SaveChangesAsync(cancellationToken);

		return ClientOperationResult.Success(ToDto(client));
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
		if (client is null)
			return false;

		_dbContext.Clients.Remove(client);
		await _dbContext.SaveChangesAsync(cancellationToken);
		return true;
	}

	private Task<bool> NameInUseAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
	{
		return _dbContext.Clients
			.AsNoTracking()
			.AnyAsync(c => c.Name.ToLower() == name.ToLower() && (excludeId == null || c.Id != excludeId), cancellationToken);
	}

	private static ClientDto ToDto(Client client) => new(
		client.Id,
		client.Name,
		client.IsActive,
		client.CreatedAt);
}
