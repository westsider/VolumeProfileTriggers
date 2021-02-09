#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	
	public class PriceRejector : Indicator
	{
		struct BarSlice {
		   public double  priceLevel;
		   public double  bidVolume;
		   public double askVolume;
		   public double delta;
			
			public BarSlice(double priceLevel, double bidVolume, double askVolume, double delta)
		    {
		        this.priceLevel = priceLevel;
		        this.bidVolume = bidVolume;
				this.askVolume = askVolume;
		        this.delta = delta;
		    }
		}; 
		
		private List<BarSlice> FullBar = new List<BarSlice>();
		private Bollinger Bollinger1;	
			
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "PriceRejector";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive		= true;
				ShowPriceRejector				= true;
				ShowAbsorbtion					= true; // max delta
				ShowExhaustion					= true; // filter the edges
				ShowUB							= true;
				AlertColorBull					= Brushes.DodgerBlue;
				AlertColorBear					= Brushes.Red;
			}
			else if (State == State.Configure)
			{
				ClearOutputWindow();
			}
			else if (State == State.DataLoaded)
			{				
				Bollinger1				= Bollinger(Close, 1.5, 14);
			}
		}

		protected override void OnBarUpdate()
		{
			 if (Bars == null)
          		return;
			 if (CurrentBar < 10 ) { return; }

			 if ( ShowExhaustion ) 
			 if (High[0] > Bollinger1.Upper[0] || Low[0] <= Bollinger1.Lower[0])
			{
					// good to trade edges
			} else { return; }
			
	        NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as    
	        NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
	       
	        if (barsType == null)
	          return;
	 
			FullBar.Clear();
			double ticks = TickSize;
			double NumPrices = Range()[0] / ticks;
			double maxDeltaPrice = 0.0;
			double minDeltaPrice = 0.0;
			
			try
	        {
	         
				/// loop thru bar prices and fill up Struct
				Print("Bar: " + CurrentBar + "\t" + Time[0] + " \trange: " + NumPrices);
				for (int index = 0; index <= NumPrices; index++)
				{
					double thisPrice = High[0] - (index * TickSize);
					double thisBid =  barsType.Volumes[CurrentBar].GetBidVolumeForPrice(thisPrice);
					double thisAsk =  barsType.Volumes[CurrentBar].GetAskVolumeForPrice(thisPrice);
					double thisDelta =  barsType.Volumes[CurrentBar].GetDeltaForPrice(thisPrice);
					BarSlice slice = new BarSlice(thisPrice, thisBid, thisAsk, thisDelta);
					FullBar.Add(slice);
					//Print(  slice.priceLevel + " \tbid: " + slice.bidVolume + " \task: " + slice.askVolume + "\t delta: " + slice.delta);
					
				}
				/// get data max and min delta from struct
				/// MARK: - TODO - Convert to boxes
				int indexOfLast = FullBar.Count - 1;
				if ( indexOfLast == 0 ) { return;} 
					var maxDelta = FullBar.Max(d => d.delta);
					var minDelta = FullBar.Min(d => d.delta);
					//Print("Min Delta: " + minDelta + " max: "  + maxDelta);
					
					foreach (var item in FullBar) {
						if (item.delta == maxDelta) {
							maxDeltaPrice = item.priceLevel;
							if ( ShowAbsorbtion ) { Draw.Dot(this, "maxDeltaPrice"+CurrentBar, false, 0, maxDeltaPrice, Brushes.DodgerBlue);}
						} else if (item.delta == minDelta) {
							minDeltaPrice = item.priceLevel;
							if ( ShowAbsorbtion ) { Draw.Dot(this, "minDeltaPrice"+CurrentBar, false, 0, minDeltaPrice, Brushes.Red);}
						}
					}
				
				
				/// Test bar low for UB or Price Rejector at Low
				/// Check for unfinished business
				//Print("first bid: " + FullBar[0].bidVolume  + " \tlast offer: " + FullBar[indexOfLast ].askVolume );

				if (FullBar[indexOfLast].askVolume != 0.0 ) { ;
					/// if vol lower then exhaustion
					//Print("Comparing last ask " + FullBar[indexOfLast].askVolume + " to ask above" + FullBar[indexOfLast -1].askVolume );
					if (FullBar[indexOfLast].askVolume  < FullBar[indexOfLast -1].askVolume ) {
						if( ShowUB ) { Draw.Text(this, "Exhastion"+CurrentBar, "UB: Exhastion", 0, Low[0] - 1 * TickSize, Brushes.Yellow);} 
					} else {
						if( ShowUB ) { Draw.Text(this, "Absorbtion"+CurrentBar, "UB: Absorbtion", 0, Low[0] - 1 * TickSize, Brushes.Yellow);}
					}
				} else {
					/// Check for Price rejector in completed auction
					/// Volume gets lower in bar
					if (FullBar[indexOfLast].bidVolume  < FullBar[indexOfLast -1].bidVolume && FullBar[indexOfLast -1].bidVolume < FullBar[indexOfLast -2].bidVolume && FullBar[indexOfLast -2].bidVolume < FullBar[indexOfLast -3].bidVolume && 
						FullBar[indexOfLast].askVolume < FullBar[indexOfLast -1].askVolume &&  FullBar[indexOfLast -1].askVolume < FullBar[indexOfLast -2].askVolume && FullBar[indexOfLast -2].askVolume < FullBar[indexOfLast -3].askVolume) {
						/// delta negative at 3 lowest levels
						if (FullBar[indexOfLast].delta <=0 && FullBar[indexOfLast - 1].delta <=0 && FullBar[indexOfLast - 2].delta <=0  ) { 
							/// diagonal agression at lowest level
							/// low bid < low + 1 offer
							if (FullBar[indexOfLast].bidVolume  < FullBar[indexOfLast - 1].askVolume )  { 
								if ( ShowPriceRejector) { Draw.Text(this, "RejectorLow"+CurrentBar, "Price Rejector", 0, Low[0] - 1 * TickSize, Brushes.DodgerBlue); }
							}
						}
					}
				}
				
				/// Test bar low for UB or Price Rejector at High
				if (FullBar[0].bidVolume != 0.0 ) { ;
					/// if vol lower then exhaustion
					//Print("Comparing last ask " + FullBar[indexOfLast].askVolume + " to ask above" + FullBar[indexOfLast -1].askVolume );
					if (FullBar[0].bidVolume  < FullBar[1].bidVolume ) {
						if( ShowUB ) { Draw.Text(this, "Exhastionh"+CurrentBar, "UB: Exhastion", 0, High[0] + 1 * TickSize, Brushes.Yellow); }
					} else {
						if( ShowUB ) { Draw.Text(this, "Absorbtionh"+CurrentBar, "UB: Absorbtion", 0, High[0] + 1 * TickSize, Brushes.Yellow);}
					}
				} else {
					/// Check for Price rejector in completed auction
					/// Volume gets lower in bar
					if (FullBar[0].bidVolume  < FullBar[1].bidVolume && FullBar[1].bidVolume < FullBar[2].bidVolume && FullBar[2].bidVolume < FullBar[3].bidVolume && 
						FullBar[0].askVolume < FullBar[1].askVolume &&  FullBar[1].askVolume < FullBar[2].askVolume && FullBar[2].askVolume < FullBar[3].askVolume) {
						/// delta negative at 3 lowest levels
						if (FullBar[0].delta >=0 && FullBar[1].delta >=0 && FullBar[2].delta >=0  ) { 
							/// diagonal agression at lowest level
							/// low bid < low + 1 offer
							if (FullBar[1].bidVolume  > FullBar[0].askVolume )  { 
								if ( ShowPriceRejector) { Draw.Text(this, "RejectorHigh"+CurrentBar, "Price Rejector", 0, High[0] + 1 * TickSize, Brushes.Red);}
							}
						}
					}
				}

	        }
	        catch{}

		}

		#region Properties
		[NinjaScriptProperty]
		[Display(Name="ShowPriceRejector", Order=1, GroupName="Parameters")]
		public bool ShowPriceRejector
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Show Max Delta", Order=2, GroupName="Parameters")]
		public bool ShowAbsorbtion
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="filter the edges", Order=3, GroupName="Parameters")]
		public bool ShowExhaustion
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="ShowUB", Order=4, GroupName="Parameters")]
		public bool ShowUB
		{ get; set; }

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="AlertColorBull", Order=5, GroupName="Parameters")]
		public Brush AlertColorBull
		{ get; set; }

		[Browsable(false)]
		public string AlertColorBullSerializable
		{
			get { return Serialize.BrushToString(AlertColorBull); }
			set { AlertColorBull = Serialize.StringToBrush(value); }
		}			

		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="AlertColorBear", Order=6, GroupName="Parameters")]
		public Brush AlertColorBear
		{ get; set; }

		[Browsable(false)]
		public string AlertColorBearSerializable
		{
			get { return Serialize.BrushToString(AlertColorBear); }
			set { AlertColorBear = Serialize.StringToBrush(value); }
		}			
		#endregion

	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private PriceRejector[] cachePriceRejector;
		public PriceRejector PriceRejector(bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, Brush alertColorBull, Brush alertColorBear)
		{
			return PriceRejector(Input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, alertColorBull, alertColorBear);
		}

		public PriceRejector PriceRejector(ISeries<double> input, bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, Brush alertColorBull, Brush alertColorBear)
		{
			if (cachePriceRejector != null)
				for (int idx = 0; idx < cachePriceRejector.Length; idx++)
					if (cachePriceRejector[idx] != null && cachePriceRejector[idx].ShowPriceRejector == showPriceRejector && cachePriceRejector[idx].ShowAbsorbtion == showAbsorbtion && cachePriceRejector[idx].ShowExhaustion == showExhaustion && cachePriceRejector[idx].ShowUB == showUB && cachePriceRejector[idx].AlertColorBull == alertColorBull && cachePriceRejector[idx].AlertColorBear == alertColorBear && cachePriceRejector[idx].EqualsInput(input))
						return cachePriceRejector[idx];
			return CacheIndicator<PriceRejector>(new PriceRejector(){ ShowPriceRejector = showPriceRejector, ShowAbsorbtion = showAbsorbtion, ShowExhaustion = showExhaustion, ShowUB = showUB, AlertColorBull = alertColorBull, AlertColorBear = alertColorBear }, input, ref cachePriceRejector);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.PriceRejector PriceRejector(bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, Brush alertColorBull, Brush alertColorBear)
		{
			return indicator.PriceRejector(Input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, alertColorBull, alertColorBear);
		}

		public Indicators.PriceRejector PriceRejector(ISeries<double> input , bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, Brush alertColorBull, Brush alertColorBear)
		{
			return indicator.PriceRejector(input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, alertColorBull, alertColorBear);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.PriceRejector PriceRejector(bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, Brush alertColorBull, Brush alertColorBear)
		{
			return indicator.PriceRejector(Input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, alertColorBull, alertColorBear);
		}

		public Indicators.PriceRejector PriceRejector(ISeries<double> input , bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, Brush alertColorBull, Brush alertColorBear)
		{
			return indicator.PriceRejector(input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, alertColorBull, alertColorBear);
		}
	}
}

#endregion
