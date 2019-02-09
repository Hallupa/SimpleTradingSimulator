# Trader Simulator
Simple Forex trader simulator for manually testing strategies.
This application is still in the early stages so is a bit raw - any bugs/suggestions please let me know.
The application currently has 30 markets each with 5 years of data built in, hence the large size of the download.

Download installed from https://github.com/Hallupa/TraderSimulator/releases

![Screenshot](https://github.com/Hallupa/TraderSimulator/blob/master/Docs/Images/Screenshot.png)

# Installation
1. Download latest version from:
https://github.com/Hallupa/TraderSimulator/releases

2. Extract the zip file
3. Run setup.exe
4. This will create a 'Trader Simulator' shortcut in your start menu

# How to use
After starting the application there will be a random chart from one of 30 markets at a random point in time.

The chart on the left shows day candles, on the right are 2 hour candles.
Clicking 'next candle' or pressing Control-F will progress one candle forward.

## Setting up a trade
Every trade has to have a stop so the trade can have a risk to reward ratio.
1. Click 'Set stop' then click a point on the chart for the stop.
2. If wanted, click 'Set limit' and click a point on the chart for the limit. Clicking 'Close half of trade at limit price' will close half the trade when the limit is reached.

## Starting the trade
The trade can either enter at market price or create an entry price.
### Market price
If the stop has been set, simply clicking 'Start long trade' or 'Start short trade' will start the trade.
The strategy will also then need to be entered or left blank.
### Entry price
To set the entry price, click 'Set entry price' then select a point on the chart.
The trade can also automatically expire by selecting an 'Order expiry candles' option.
Then click 'Start long trade' or 'Start short trade' to begin. When progressing through the candles, if the entry price is hit the trade will start.
### Editing the trade
When the trade is running, clicking 'Close' at any point will close the trade at the current price.
The stop/limit can also be adjusted by clicking the 'Set stop'/'Set limit' options then clicking the chart.

## Results
Under the charts is the results section.
All the results are shown in risk to reward ratios, or R, and shows statistics such as % trades that made a positve R, R sum, expectancy, etc.
In the drop down listbox it also gives options such as showing results grouped by strategies, markets, etc.

## Completed trades
Under the results section shows the completed trades and their details

# License

This library is release under GNU General Public License v3.0.

Copyright (c) 2019 Oliver Wickenden

https://github.com/Hallupa/TraderSimulator/blob/master/LICENSE
