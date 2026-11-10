namespace MemByte.Application.Clients;

/// <summary>
/// Servicio de aplicación para la gestión de la ficha de clientes
/// (creación, edición, consulta y activación/desactivación), desacoplado de
/// la capa de persistencia concreta. Es la base mínima del futuro módulo de
/// "Gestión de Proyectos y Clientes": por ahora sólo agrupa usuarios
/// externos (ver MemByte.Application.Users), sin proyectos ni facturación.
/// </summary>
public interface IClientService
{
	Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<ClientOperationResult> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default);

	Task<ClientOperationResult> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
