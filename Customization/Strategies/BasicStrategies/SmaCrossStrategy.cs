using System;
using System.Collections.Generic;
using StockSharp.Algo.Indicators;
using StockSharp.Algo.Strategies;
using StockSharp.Messages;
using StockSharp.BusinessEntities;

namespace BasicStrategies
{
	public class SmaCrossStrategy : Strategy
	{
		private readonly StrategyParam<int> _fastPeriod;
		private readonly StrategyParam<int> _slowPeriod;
		private readonly StrategyParam<decimal> _tradeVolume;
		private readonly StrategyParam<DataType> _candleType;

		private decimal _prevFastValue;
		private decimal _prevSlowValue;
		private bool _isFirstValue = true;

		public int FastPeriod
		{
			get => _fastPeriod.Value;
			set => _fastPeriod.Value = value;
		}

		public int SlowPeriod
		{
			get => _slowPeriod.Value;
			set => _slowPeriod.Value = value;
		}

		public decimal TradeVolume
		{
			get => _tradeVolume.Value;
			set => _tradeVolume.Value = value;
		}

		public DataType CandleType
		{
			get => _candleType.Value;
			set => _candleType.Value = value;
		}

		public SmaCrossStrategy()
		{
			_fastPeriod = Param(nameof(FastPeriod), 10)
				.SetGreaterThanZero()
				.SetDisplay("Fast SMA Period", "Period for fast Simple Moving Average", "Indicators")
				.SetCanOptimize(true)
				.SetOptimize(5, 25, 5);

			_slowPeriod = Param(nameof(SlowPeriod), 20)
				.SetGreaterThanZero()
				.SetDisplay("Slow SMA Period", "Period for slow Simple Moving Average", "Indicators")
				.SetCanOptimize(true)
				.SetOptimize(10, 50, 5);

			_tradeVolume = Param(nameof(TradeVolume), 1m)
				.SetGreaterThanZero()
				.SetDisplay("Trade Volume", "Trade volume size", "Trading")
				.SetCanOptimize(true)
				.SetOptimize(1, 10, 1);

			_candleType = Param(nameof(CandleType), TimeSpan.FromMinutes(5).TimeFrame())
				.SetDisplay("Candle Type", "Type of candles to use for analysis", "General");
		}

		public override IEnumerable<(Security sec, DataType dt)> GetWorkingSecurities()
		{
			return new[] { (Security, CandleType) };
		}

		protected override void OnStarted(DateTimeOffset time)
		{
			base.OnStarted(time);

			var fastSma = new SimpleMovingAverage { Length = FastPeriod };
			var slowSma = new SimpleMovingAverage { Length = SlowPeriod };

			Indicators.Add(fastSma);
			Indicators.Add(slowSma);

			var subscription = SubscribeCandles(CandleType);
			subscription
				.Bind(fastSma, slowSma, ProcessCandle)
				.Start();

			var area = CreateChartArea();
			if (area != null)
			{
				DrawCandles(area, subscription);
				DrawIndicator(area, fastSma, System.Drawing.Color.Orange);
				DrawIndicator(area, slowSma, System.Drawing.Color.Blue);
				DrawOwnTrades(area);
			}
		}

		private void ProcessCandle(ICandleMessage candle, decimal fastValue, decimal slowValue)
		{
			if (candle.State != CandleStates.Finished)
				return;

			if (!IsFormedAndOnlineAndAllowTrading())
				return;

			if (_isFirstValue)
			{
				_prevFastValue = fastValue;
				_prevSlowValue = slowValue;
				_isFirstValue = false;
				return;
			}

			var isFastAboveCurrent = fastValue > slowValue;
			var isFastAbovePrev = _prevFastValue > _prevSlowValue;

			_prevFastValue = fastValue;
			_prevSlowValue = slowValue;

			if (isFastAboveCurrent == isFastAbovePrev)
				return;

			if (isFastAboveCurrent)
				BuyMarket(TradeVolume);
			else
				SellMarket(TradeVolume);
		}
	}
}