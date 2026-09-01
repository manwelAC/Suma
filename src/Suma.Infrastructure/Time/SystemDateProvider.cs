using Suma.Application.Abstractions.Time;

namespace Suma.Infrastructure.Time;

public sealed class SystemDateProvider : IDateProvider
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
