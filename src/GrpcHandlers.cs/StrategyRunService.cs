using Grpc.Core;
using Retsuko.Strategies.Services;

namespace Retsuko.Strategies.GrpcHandlers;

public class StrategyRunService : GStrategyRunner.GStrategyRunnerBase {
  public override async Task Run(IAsyncStreamReader<StrategyInput> requestStream, IServerStreamWriter<StrategyOutput> responseStream, ServerCallContext context) {
    await requestStream.MoveNext();
    var create = requestStream.Current.Create;
    if (create == null) {
      throw new RpcException(new Status(StatusCode.InvalidArgument, "First message must be Create"));
    }

    var strategy = StrategyLoader.CreateStrategy(create.Name, create.Config);
    if (strategy == null) {
      throw new RpcException(new Status(StatusCode.InvalidArgument, $"Strategy {create.Name} not found"));
    }

    if (!string.IsNullOrEmpty(create.State)) {
      strategy.Deserialize(create.State);
    }

    while (await requestStream.MoveNext(context.CancellationToken) && !context.CancellationToken.IsCancellationRequested) {
      var input = requestStream.Current;
      if (input.Preload != null) {
        await strategy.Preload(input.Preload.Candle);
      } else if (input.Update != null) {
        var signal = await strategy.Update(input.Update.Candle);
        await responseStream.WriteAsync(new StrategyOutput {
          Signal = signal == null ? null : new StrategyOutputSignal {
            Kind = (int)signal.kind,
            Confidence = signal.confidence
          },
        });
      } else {
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown message type"));
      }
    }

    await responseStream.WriteAsync(new StrategyOutput {
      State = new StrategyOutputState {
        State = strategy.Serialize(),
        Debug = "",
      },
    });
  }
}
