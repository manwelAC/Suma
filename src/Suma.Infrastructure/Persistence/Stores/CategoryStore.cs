using Microsoft.EntityFrameworkCore;
using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Categories;

namespace Suma.Infrastructure.Persistence.Stores;

public sealed class CategoryStore(SumaDbContext context) : ICategoryStore
{
    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Categories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);
}
