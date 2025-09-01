using Google.Protobuf.Collections;
using Grpc.Core;
using Retsuko.Strategies.Services;

namespace Retsuko.Strategies.GrpcHandlers;

public class StrategyRunService : GStrategyRunner.GStrategyRunnerBase {
  public override async Task Run(IAsyncStreamReader<StrategyInput> requestStream, IServerStreamWriter<StrategyOutput> responseStream, ServerCallContext context) {
    var tracer = MyTracer.Tracer;
    using var createSpan = tracer.StartActiveSpan("StrategyRunService.Run.Create");

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

    createSpan.End();

    using var processSpan = tracer.StartActiveSpan("StrategyRunService.Run.Process");
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
        }, context.CancellationToken);
      } else {
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown message type"));
      }
    }

    processSpan.End();

    using var responseSpan = tracer.StartActiveSpan("StrategyRunService.Run.Response");
    await responseStream.WriteAsync(new StrategyOutput {
      State = new StrategyOutputState {
        State = strategy.Serialize(),
        Debug = "",
      },
    }, context.CancellationToken);
    responseSpan.End();
  }

  public override async Task RunLazy(IAsyncStreamReader<StrategyInputBatch> requestStream, IServerStreamWriter<StrategyLazyOutputBatch> responseStream, ServerCallContext context) {
    var tracer = MyTracer.Tracer;
    using var createSpan = tracer.StartActiveSpan("StrategyRunService.RunLazy.Create");

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

    createSpan.End();

    using var processSpan = tracer.StartActiveSpan("StrategyRunService.RunLazy.Process");
    var signalResponses = new Queue<StrategyOutputSignalWithCandle>();
    while (await requestStream.MoveNext(context.CancellationToken) && !context.CancellationToken.IsCancellationRequested) {
      var input = requestStream.Current;
      if (input.Preload != null) {
        foreach (var candle in input.Preload.Candles) {
          await strategy.Preload(candle);
        }
      } else if (input.Update != null) {
        foreach (var candle in input.Update.Candles) {
          var signal = await strategy.Update(candle);
          if (signal != null) {
            signalResponses.Enqueue(new StrategyOutputSignalWithCandle {
              Candle = candle,
              Kind = (int)signal.kind,
              Confidence = signal.confidence
            });
          }
        }

      } else {
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown message type"));
      }
    }

    processSpan.End();

    using var signalResponseSpan = tracer.StartActiveSpan("StrategyRunService.RunLazy.SignalResponse");
    var chunks = signalResponses.Chunk(200);
    foreach (var chunk in chunks) {
      if (context.CancellationToken.IsCancellationRequested) {
        break;
      }

      var output = new StrategyLazyOutputBatch();
      output.Outputs.AddRange(chunk.Select(response => new StrategyLazyOutput { Signal = response }));
      await responseStream.WriteAsync(output, context.CancellationToken);
    }
    signalResponseSpan.End();

    using var responseSpan = tracer.StartActiveSpan("StrategyRunService.RunLazy.Response");

    var responseOutput = new StrategyLazyOutputBatch();
    responseOutput.Outputs.Add(new StrategyLazyOutput {
      State = new StrategyOutputState {
        State = strategy.Serialize(),
        Debug = "",
      },
    });
    await responseStream.WriteAsync(responseOutput, context.CancellationToken);
    responseSpan.End();
  }
}
