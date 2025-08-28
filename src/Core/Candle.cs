namespace Retsuko.Strategies.Core;

public record struct Candle(
  Market market,
  int symbolId,
  KlineInterval interval,
  DateTime ts,
  double open,
  double high,
  double low,
  double close,
  double volume
);
