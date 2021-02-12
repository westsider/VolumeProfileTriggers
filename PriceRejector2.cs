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

using SharpDX.Direct2D1;
using SharpDX;
using SharpDX.DirectWrite;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class BarRenderTest : Indicator
	{
		struct BarSlice {
		   public double  priceLevel;
		   public double  bidVolume;
		   public double askVolume;
		   public double delta;
		   public double maxDelta ;
		   public double minDelta;
			
			public BarSlice(double priceLevel, double bidVolume, double askVolume, double delta, double maxDelta, double minDelta)
		    {
		        this.priceLevel = priceLevel;
		        this.bidVolume = bidVolume;
				this.askVolume = askVolume;
		        this.delta = delta;
				this.maxDelta = maxDelta;
				this.minDelta = minDelta;
		    }
		}; 
		
		private List<BarSlice> FullBar = new List<BarSlice>();
		private List<Double> MaxDelta = new List<Double>();
		private List<Double> MinDelta = new List<Double>();
		private Bollinger Bollinger1;	
		private bool GoodToTrade = true;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "Bar Render Test";
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
				IsSuspendedWhileInactive					= true;
				ShowPriceRejector				= true;
				ShowAbsorbtion					= true; // max delta
				ShowExhaustion					= true; // filter the edges
				ShowUB							= true;
				//AlertColorBull					= Brushes.DodgerBlue;
				//AlertColorBear					= Brushes.Red;
				AInt							= 30;
				//AreaBrush 				= System.Windows.Media.Brushes.DodgerBlue;
			}
			else if (State == State.Configure)
			{
				ClearOutputWindow();
				AddVolumetric("ES 03-21", BarsPeriodType.Minute, AInt, VolumetricDeltaType.BidAsk, 1);
			}
			else if (State == State.DataLoaded)
			{				
				Bollinger1				= Bollinger(Close, 1.5, 14);
			}
		}

		protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			  BrushProperties bp2=new BrushProperties();
			  bp2.Opacity= 1.0f;
			  // MARK - TODO - Make this number auto scale
			  int priceAdjuster = 18; // larger number is lower
			  int priceWidth = (priceAdjuster * -2 );
			  if (ChartBars != null)
			  {
					Print("\n-------------------------------------------------------------------------------");
				    for (int barIndex = ChartBars.FromIndex; barIndex <= ChartBars.ToIndex; barIndex++)
				    {
						
						if (barIndex < MaxDelta.Count ) {
							/// max delta
							int y=chartScale.GetYByValue(MaxDelta[barIndex ]) +priceAdjuster;
							//int y2 = chartScale.GetYByValue(MaxDelta[barIndex ] + TickSize);
							int x=chartControl.GetXByBarIndex(ChartBars,barIndex)-(int)chartControl.BarWidth;
				      	    //Print("bar: "+ barIndex + " high: " + High.GetValueAt(barIndex) + " " + FullBar.Count + " median " + MaxDelta[barIndex] + ", y " + y + " x " + x + " tick = " + TickSize + " y2 " + y2);
							SharpDX.Direct2D1.SolidColorBrush frame=new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,SharpDX.Color.DimGray,bp2);
							RenderTarget.DrawRectangle(new RectangleF(x,y,(int)chartControl.BarWidth*2,(float)(priceWidth)),frame,2);	
							/// min delta
							int y2=chartScale.GetYByValue(MinDelta[barIndex ])+priceAdjuster;
							//int y4 = chartScale.GetYByValue(MinDelta[barIndex ] + TickSize);
							int x2=chartControl.GetXByBarIndex(ChartBars,barIndex) -  (int)chartControl.BarWidth;
				      	    //Print("bar: "+ barIndex + " high: " + High.GetValueAt(barIndex) + " " + FullBar.Count + " median " + MaxDelta[barIndex] + ", y " + y + " x " + x + " tick = " + TickSize + " y2 " + y2);
							SharpDX.Direct2D1.SolidColorBrush frame2=new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,SharpDX.Color.Red,bp2);
							RenderTarget.DrawRectangle(new RectangleF(x2,y2,(int)chartControl.BarWidth*2,(float)(priceWidth)),frame2,2);	
						}
				    }
				}
		}
		
		protected override void OnBarUpdate()
		{
			if (BarsInProgress == 1 ) {
				SetEdgeFilter();
				PopulateList() ;
			}
			
			//MARK: - TODO - Add 1 more lists that shoe +1 -1 for price rejector
			// MARK: - TODO - Make box size auto resizing
		}
		
		private void SetEdgeFilter() {
			    if ( ShowExhaustion )  {
				    if (High[0] > Bollinger1.Upper[0] || Low[0] <= Bollinger1.Lower[0])
					{
						GoodToTrade = true;
					} else {
						GoodToTrade = false;
					}
				}
		}
		
		private void PopulateList() {
			NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType barsType = Bars.BarsSeries.BarsType as    
	        NinjaTrader.NinjaScript.BarsTypes.VolumetricBarsType;
			FullBar.Clear();
			double ticks = TickSize;
			double NumPrices = Range()[0] / ticks;
			double maxDeltaPrice = 0.0;
			double minDeltaPrice = 0.0;
			
			try
	        {
	         
				// loop thru bar prices and fill up Struct
				Print("Bar: " + CurrentBar + "\t" + Time[0] + " \trange: " + NumPrices);
				// needed to move the index forward
				FullBar.Add(new BarSlice(0.0, 0.0, 0.0, 0.0, 0.0, 0.0));
				for (int index = 0; index <= NumPrices; index++)
				{
					double thisPrice = High[0] - (index * TickSize);
					double thisBid =  barsType.Volumes[CurrentBar].GetBidVolumeForPrice(thisPrice);
					double thisAsk =  barsType.Volumes[CurrentBar].GetAskVolumeForPrice(thisPrice);
					double thisDelta =  barsType.Volumes[CurrentBar].GetDeltaForPrice(thisPrice);
					BarSlice slice = new BarSlice(thisPrice, thisBid, thisAsk, thisDelta, 0.0, 0.0);
					FullBar.Add(slice);
					Print(  slice.priceLevel + " \tbid: " + slice.bidVolume + " \task: " + slice.askVolume + "\t delta: " + slice.delta);
					
				}
				
				
				//FullBar.Last().miinDelta = FullBar.Min(d => d.delta);
				// get data max and min delta from struct
				// MARK: - TODO - Convert to boxes
				int indexOfLast = FullBar.Count - 1;
				if ( indexOfLast == 0 ) { return;} 
					var maxDelta = FullBar.Max(d => d.delta);
					var minDelta = FullBar.Min(d => d.delta);
				   
				  
					Print("Min Delta: " + minDelta + " max: "  + maxDelta);
					
					foreach (var item in FullBar) {
						if (item.delta == maxDelta) {
							maxDeltaPrice = item.priceLevel;
							MaxDelta.Add(maxDeltaPrice);
							if ( ShowAbsorbtion ) { 
								//Draw.Dot(this, "maxDeltaPrice"+CurrentBar, false, 0, maxDeltaPrice, Brushes.DodgerBlue);
								//Draw.Rectangle(this,  "maxDeltaPrice"+CurrentBar, false, 0, maxDeltaPrice - (TickSize * 1 ), -1, maxDeltaPrice, Brushes.DodgerBlue , Brushes.Transparent, 2);
							}
						} else if (item.delta == minDelta) {
							minDeltaPrice = item.priceLevel;
							 MinDelta.Add(minDeltaPrice);
							if ( ShowAbsorbtion ) { 
								//Draw.Dot(this, "minDeltaPrice"+CurrentBar, false, 0, minDeltaPrice, Brushes.Red);
								//Draw.Rectangle(this,  "minDeltaPrice"+CurrentBar, false, 0, minDeltaPrice - (TickSize * 1 ), -1, minDeltaPrice, Brushes.Red, Brushes.Transparent, 2);
								//Draw.Rectangle(this, "tag1", false, 10, Low[10] - TickSize, 5, High[5] + TickSize, Brushes.PaleGreen, Brushes.PaleGreen, 2);
							}
						}
					}
				
				
				// Test bar low for UB or Price Rejector at Low
				// Check for unfinished business
				Print("first bid: " + FullBar[0].bidVolume  + " \tlast offer: " + FullBar[indexOfLast ].askVolume );

				if (FullBar[indexOfLast].askVolume != 0.0 ) { ;
					// if vol lower then exhaustion
					Print("Comparing last ask " + FullBar[indexOfLast].askVolume + " to ask above" + FullBar[indexOfLast -1].askVolume );
					if (FullBar[indexOfLast].askVolume  < FullBar[indexOfLast -1].askVolume ) {
						if( ShowUB && GoodToTrade) { Draw.Text(this, "Exhastion"+CurrentBar, "UB: Exhastion", 0, Low[0] - 1 * TickSize, Brushes.Yellow);} 
					} else {
						if( ShowUB && GoodToTrade) { Draw.Text(this, "Absorbtion"+CurrentBar, "UB: Absorbtion", 0, Low[0] - 1 * TickSize, Brushes.Yellow);}
					}
				} else {
					// Check for Price rejector in completed auction
					// Volume gets lower in bar
					if (FullBar[indexOfLast].bidVolume  < FullBar[indexOfLast -1].bidVolume && FullBar[indexOfLast -1].bidVolume < FullBar[indexOfLast -2].bidVolume && FullBar[indexOfLast -2].bidVolume < FullBar[indexOfLast -3].bidVolume && 
						FullBar[indexOfLast].askVolume < FullBar[indexOfLast -1].askVolume &&  FullBar[indexOfLast -1].askVolume < FullBar[indexOfLast -2].askVolume && FullBar[indexOfLast -2].askVolume < FullBar[indexOfLast -3].askVolume) {
						// delta negative at 3 lowest levels
						if (FullBar[indexOfLast].delta <=0 && FullBar[indexOfLast - 1].delta <=0 && FullBar[indexOfLast - 2].delta <=0  ) { 
							// diagonal agression at lowest level
						//	/ low bid < low + 1 offer
							if (FullBar[indexOfLast].bidVolume  < FullBar[indexOfLast - 1].askVolume )  { 
								if ( ShowPriceRejector && GoodToTrade) { 
									Draw.Text(this, "RejectorLow"+CurrentBar, "Price Rejector", 0, Low[0] - 1 * TickSize, Brushes.DimGray);
									//Draw.Rectangle(this,  "RejectorLow"+CurrentBar, false, 1, Low[0] - TickSize, -1, Low[0] + (TickSize * 3), Brushes.DodgerBlue, Brushes.Transparent, 1);	
								}
							}
						}
					}
				}
				
				// Test bar low for UB or Price Rejector at High
				if (FullBar[0].bidVolume != 0.0 ) { ;
					// if vol lower then exhaustion
					Print("Comparing last ask " + FullBar[indexOfLast].askVolume + " to ask above" + FullBar[indexOfLast -1].askVolume );
					if (FullBar[0].bidVolume  < FullBar[1].bidVolume ) {
						if( ShowUB && GoodToTrade) { Draw.Text(this, "Exhastionh"+CurrentBar, "UB: Exhastion", 0, High[0] + 1 * TickSize, Brushes.Yellow); }
					} else {
						if( ShowUB && GoodToTrade) { Draw.Text(this, "Absorbtionh"+CurrentBar, "UB: Absorbtion", 0, High[0] + 1 * TickSize, Brushes.Yellow);}
					}
				} else {
					// Check for Price rejector in completed auction
					// Volume gets lower in bar
					if (FullBar[0].bidVolume  < FullBar[1].bidVolume && FullBar[1].bidVolume < FullBar[2].bidVolume && FullBar[2].bidVolume < FullBar[3].bidVolume && 
						FullBar[0].askVolume < FullBar[1].askVolume &&  FullBar[1].askVolume < FullBar[2].askVolume && FullBar[2].askVolume < FullBar[3].askVolume) {
					//	// delta negative at 3 lowest levels
						if (FullBar[0].delta >=0 && FullBar[1].delta >=0 && FullBar[2].delta >=0  ) { 
							// diagonal agression at lowest level
							// low bid < low + 1 offer
							if (FullBar[1].bidVolume  > FullBar[0].askVolume )  { 
								if ( ShowPriceRejector && GoodToTrade) { 
									Draw.Text(this, "RejectorHigh"+CurrentBar, "Price Rejector", 0, High[0] + 1 * TickSize, Brushes.Red);
									//Draw.Rectangle(this,  "RejectorHigh"+CurrentBar, false, 1, High[0] -(TickSize * 3), -1, High[0] + TickSize , Brushes.Red, Brushes.Transparent, 1);	
								}
								
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

//		[NinjaScriptProperty]
//		[XmlIgnore]
//		[Display(Name="AlertColorBull", Order=5, GroupName="Parameters")]
//		public Brush AlertColorBull
//		{ get; set; }

//		[Browsable(false)]
//		public string AlertColorBullSerializable
//		{
//			get { return Serialize.BrushToString(AlertColorBull); }
//			set { AlertColorBull = Serialize.StringToBrush(value); }
//		}			

//		[NinjaScriptProperty]
//		[XmlIgnore]
//		[Display(Name="AlertColorBear", Order=6, GroupName="Parameters")]
//		public Brush AlertColorBear
//		{ get; set; }

//		[Browsable(false)]
//		public string AlertColorBearSerializable
//		{
//			get { return Serialize.BrushToString(AlertColorBear); }
//			set { AlertColorBear = Serialize.StringToBrush(value); }
//		}			
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Time Series Minutes", Order=3, GroupName="Parameters")]
		public int AInt
		{ get; set; }
		
		// quick draw dx
		
//		[XmlIgnore]
//		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolShapesAreaBrush", GroupName = "NinjaScriptGeneral")]
//		public System.Windows.Media.Brush AreaBrush
//		{
//			get { return areaBrush; }
//			set
//			{
//				areaBrush = value;
//				if (areaBrush != null)
//				{
//					if (areaBrush.IsFrozen)
//						areaBrush = areaBrush.Clone();
//					areaBrush.Opacity = areaOpacity / 100d;
//					areaBrush.Freeze();
//				}
//			}
//		}

//		[Browsable(false)]
//		public string AreaBrushSerialize
//		{
//			get { return Serialize.BrushToString(AreaBrush); }
//			set { AreaBrush = Serialize.StringToBrush(value); }
//		}
		
//		[Range(0, 100)]
//		[Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAreaOpacity", GroupName = "NinjaScriptGeneral")]
//		public int AreaOpacity
//		{
//			get { return areaOpacity; }
//			set
//			{
//				areaOpacity = Math.Max(0, Math.Min(100, value));
//				if (areaBrush != null)
//				{
//					System.Windows.Media.Brush newBrush		= areaBrush.Clone();
//					newBrush.Opacity	= areaOpacity / 100d;
//					newBrush.Freeze();
//					areaBrush			= newBrush;
//				}
//			}
//		}	
		#endregion
		
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private BarRenderTest[] cacheBarRenderTest;
		public BarRenderTest BarRenderTest(bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, int aInt)
		{
			return BarRenderTest(Input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, aInt);
		}

		public BarRenderTest BarRenderTest(ISeries<double> input, bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, int aInt)
		{
			if (cacheBarRenderTest != null)
				for (int idx = 0; idx < cacheBarRenderTest.Length; idx++)
					if (cacheBarRenderTest[idx] != null && cacheBarRenderTest[idx].ShowPriceRejector == showPriceRejector && cacheBarRenderTest[idx].ShowAbsorbtion == showAbsorbtion && cacheBarRenderTest[idx].ShowExhaustion == showExhaustion && cacheBarRenderTest[idx].ShowUB == showUB && cacheBarRenderTest[idx].AInt == aInt && cacheBarRenderTest[idx].EqualsInput(input))
						return cacheBarRenderTest[idx];
			return CacheIndicator<BarRenderTest>(new BarRenderTest(){ ShowPriceRejector = showPriceRejector, ShowAbsorbtion = showAbsorbtion, ShowExhaustion = showExhaustion, ShowUB = showUB, AInt = aInt }, input, ref cacheBarRenderTest);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.BarRenderTest BarRenderTest(bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, int aInt)
		{
			return indicator.BarRenderTest(Input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, aInt);
		}

		public Indicators.BarRenderTest BarRenderTest(ISeries<double> input , bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, int aInt)
		{
			return indicator.BarRenderTest(input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, aInt);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.BarRenderTest BarRenderTest(bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, int aInt)
		{
			return indicator.BarRenderTest(Input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, aInt);
		}

		public Indicators.BarRenderTest BarRenderTest(ISeries<double> input , bool showPriceRejector, bool showAbsorbtion, bool showExhaustion, bool showUB, int aInt)
		{
			return indicator.BarRenderTest(input, showPriceRejector, showAbsorbtion, showExhaustion, showUB, aInt);
		}
	}
}

#endregion
