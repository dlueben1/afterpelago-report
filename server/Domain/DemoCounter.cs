namespace Afterpelago.Domain;

public sealed class DemoCounter
{
    public int Id { get; set; }

    public long Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
