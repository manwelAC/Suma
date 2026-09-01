using Suma.Domain.Categories;

namespace Suma.Application.Abstractions.Persistence;

public interface ICategoryStore
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
