using Suma.Domain.Categories;

namespace Suma.Application.Abstractions.Persistence;

public interface ICategoryStore
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> HasActiveChildrenAsync(Guid parentCategoryId, CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);
}
