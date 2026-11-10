namespace MemByte.Application.Employees;

/// <summary>
/// Servicio de aplicación para la gestión de fichas de empleados
/// (creación, edición, consulta y baja), desacoplado de la capa de
/// persistencia concreta.
/// </summary>
public interface IEmployeeService
{
	Task<IReadOnlyList<EmployeeDto>> GetAllAsync(CancellationToken cancellationToken = default);

	Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

	Task<EmployeeOperationResult> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);

	Task<EmployeeOperationResult> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);

	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
