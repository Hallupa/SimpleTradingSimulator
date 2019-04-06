# SimpleTradingSimulator
## SimpleTradingSimulator is a Forex trading simulator for manually testing trading strategies and analysing results.
The application allows stepping through a market at any speed allowing trades to be simulated across many years of data very quickly.
It currently has 30 markets each with 5 years of data from FXCM built in, hence the large size of the download.

![Screenshot](https://github.com/Hallupa/SimpleTradingSimulator/blob/master/Docs/Images/Screenshot.png)

# Installation
1. Download latest version from:
https://github.com/Hallupa/SimpleTradingSimulator/releases

2. Run 'TradingSimulatorInstaller.msi' which will install the application along with shortcuts on the desktop and start menu

# How to use
After starting the application there will be a random chart from one of 30 markets at a random point in time.

The chart on the left shows day candles, on the right are 2 hour candles.
Clicking 'next candle' or pressing Control-F will progress one candle forward.
Clicking 'New chart' will select another random market and time.

## Setting up a trade
Clicking 'long trade' or 'short trade' will bring up the setup trade window.
Every trade has to have a stop so the trade can have a risk to reward ratio.
1. Click 'Set stop' then click a point on the chart for the stop.
2. If wanted, click 'Set limit' and click a point on the chart for the limit. Clicking 'Close half of trade at limit price' will close half the trade when the limit is reached.

### Market price
If the stop has been set, simply clicking 'OK' will start the trade.
### Entry price
To set the entry price, click 'Set entry price' then select a point on the chart.
Then click 'OK' to begin. When progressing through the candles, if the entry price is hit the trade will start.

## Editing the trade
Clicking a trade in the 'Trades' section then clicking the toolbar option 'Edit trade' will bring up the edit trade window.
In there the trade can be closed or the stop/limit can also be adjusted.

## Results
In the Analyis section all the results are shown in risk to reward ratios, or R, and shows statistics such as % trades that made a positve R, expectancy, etc.

# License

This library is release under GNU General Public License v3.0.

Copyright (c) 2019 Oliver Wickenden

https://github.com/Hallupa/TraderSimulator/blob/master/LICENSE
