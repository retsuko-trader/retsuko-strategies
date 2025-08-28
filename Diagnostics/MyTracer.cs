using OpenTelemetry.Trace;

namespace Retsuko.Strategies.Diagnostics;

public static class MyTracer {
  const string SERVICE_NAME = "retsuko-strategy";

  public static readonly Tracer Tracer = TracerProvider.Default.GetTracer(SERVICE_NAME);
}
