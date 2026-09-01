using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Categories;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class CategoryStore(SumaDbContext context) : ICategoryStore
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Categories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Categories.AsNoTracking()
            .OrderBy(category => category.TransactionKind)
            .ThenBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> HasActiveChildrenAsync(Guid parentCategoryId, CancellationToken cancellationToken = default) =>
        context.Categories.AnyAsync(
            category => category.ParentCategoryId == parentCategoryId && !category.IsArchived,
            cancellationToken);

    public Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        context.Categories.AddAsync(category, cancellationToken).AsTask();
}
