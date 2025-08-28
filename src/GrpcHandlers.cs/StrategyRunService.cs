using Grpc.Core;
using Retsuko.Strategies.Services;

namespace Retsuko.Strategies.GrpcHandlers;

public class StrategyRunService : GStrategyRunner.GStrategyRunnerBase {
  public override Task<StrategyRun> Create(StrategyCreate request, ServerCallContext context) {
    return base.Create(request, context);
  }

  public override Task<Empty> Preload(StrategyPush request, ServerCallContext context) {
    return base.Preload(request, context);
  }

  public override Task<Empty> Process(StrategyPush request, ServerCallContext context) {
    return base.Process(request, context);
  }

  public override Task<StrategyState> End(StrategyRun request, ServerCallContext context) {
    return base.End(request, context);
  }
}
