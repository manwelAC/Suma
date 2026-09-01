using Suma.Application.Abstractions.Time;

namespace Suma.Application.Tests.TestDoubles;

internal sealed class FakeDateProvider(DateOnly today) : IDateProvider
{
    public DateOnly Today { get; } = today;
}
