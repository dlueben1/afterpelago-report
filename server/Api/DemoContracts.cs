namespace Afterpelago.Api;

public sealed record SamplePoint(string Label, double Value);

public sealed record ServerDataResponse(
    string Greeting,
    DateTimeOffset ServerTime,
    double RandomValue,
    IReadOnlyList<SamplePoint> Samples);

public sealed record CounterResponse(long Value, DateTimeOffset UpdatedAt);
