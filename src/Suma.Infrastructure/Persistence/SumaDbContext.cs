using Microsoft.EntityFrameworkCore;

namespace Suma.Infrastructure.Persistence;

public sealed class SumaDbContext(DbContextOptions<SumaDbContext> options) : DbContext(options);
