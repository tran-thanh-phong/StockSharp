using Ecng.Xaml;

namespace StockSharp.Samples.Basic.Orders;

using System.Windows;
using System.IO;

using Ecng.Serialization;
using Ecng.Configuration;
using Ecng.Collections;
using Ecng.Logging;

using StockSharp.Algo;
using StockSharp.BusinessEntities;
using StockSharp.Configuration;
using StockSharp.Messages;
using StockSharp.Xaml;
using System;

public partial class MainWindow
{
	private readonly Connector _connector = new();
	private const string _connectorFile = "ConnectorFile.json";
	private readonly LogManager _logManager = new();

	public MainWindow()
	{
		InitializeComponent();

		// registering all connectors
		ConfigManager.RegisterService<IMessageAdapterProvider>(new InMemoryMessageAdapterProvider(_connector.Adapter.InnerAdapters));

		// configure logging
		_logManager.FlushInterval = TimeSpan.FromMilliseconds(1); // immediate flush
		_logManager.Listeners.Add(new GuiLogListener(LogMonitor));
		_logManager.Sources.Add(_connector);

		if (File.Exists(_connectorFile))
		{
			_connector.Load(_connectorFile.Deserialize<SettingsStorage>());
		}
	}

	private void Setting_Click(object sender, RoutedEventArgs e)
	{
		if (_connector.Configure(this))
		{
			_connector.Save().Serialize(_connectorFile);
		}
	}
	

	private void Connect_Click(object sender, RoutedEventArgs e)
	{
		// SecurityEditor.SecurityProvider = _connector;
		// PortfolioEditor.Portfolios = new PortfolioDataSource(_connector);
		
		try
		{
            _connector.OrderReceived += OrderReceived;

            _connector.OrderRegisterFailReceived += (s, f) => OrderGrid.AddRegistrationFail(f);
            _connector.OwnTradeReceived += (s, t) => MyTradeGrid.Trades.TryAdd(t);

			// Subscribe to connection events
			_connector.Connected += () =>
			{
				Dispatcher.Invoke(() =>
				{
					SecurityEditor.SecurityProvider = _connector;
					PortfolioEditor.Portfolios = new PortfolioDataSource(_connector);
				});		
				_connector.AddInfoLog("[Connect_Click] Connector connected.");
			};

            _connector.Connect();
        }
        catch (Exception ex)
		{
			_connector.AddErrorLog(ex);
		}
	}
	
    private void OrderReceived(Subscription s, Order o)
	{
        OrderGrid.Orders.TryAdd(o);
    }


    private void Buy_Click(object sender, RoutedEventArgs e)
	{
		var order = new Order
		{
			Security = SecurityEditor.SelectedSecurity,
			Portfolio = PortfolioEditor.SelectedPortfolio,
			Price = decimal.Parse(TextBoxPrice.Text),
			Volume = 1,
			Side = Sides.Buy,
		};

		_connector.RegisterOrder(order);
    }


	private void Sell_Click(object sender, RoutedEventArgs e)
	{
		var order = new Order
		{
			Security = SecurityEditor.SelectedSecurity,
			Portfolio = PortfolioEditor.SelectedPortfolio,
			Price = decimal.Parse(TextBoxPrice.Text),
			Volume = 1,
			Side = Sides.Sell,
		};

		_connector.RegisterOrder(order);
	}
}