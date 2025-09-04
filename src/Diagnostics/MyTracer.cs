using OpenTelemetry.Trace;

namespace Retsuko.Strategies.Diagnostics;

public static class MyTracer {
  public static string SERVICE_NAME;

  public static readonly Tracer Tracer = TracerProvider.Default.GetTracer(SERVICE_NAME);
}
