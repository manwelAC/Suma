namespace Suma.Application.Abstractions.Time;

public interface IDateProvider
{
    DateOnly Today { get; }
}
