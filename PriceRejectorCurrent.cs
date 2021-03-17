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
	public class PriceRejectorCurrent : Indicator
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
		
		struct RejectorSignal {
			public int  direction;
			public double low;
		    public double  high;
		   
			public RejectorSignal(int direction, double low, double high)
			{
				this.direction = direction;
		        this.low = low;
				this.high = high;
			}
		}
		
		private List<BarSlice> FullBar = new List<BarSlice>();
		private List<double> MaxDelta = new List<double>();
		private List<double> MinDelta = new List<double>();
		private List<int> PriceRejector = new List<int>();
		private List<RejectorSignal> RejectorSignals = new List<RejectorSignal>();
		private bool GoodToTrade = true;
		private Bollinger Bollinger1;	
		
		// cureent bar draw objects
		private double lastPRhigh = 0;
		private double lastABhigh = 0;
		private double lastUBhigh = 0;
		private double lastPRLow = 0;
		private double lastABLow = 0;
		private double lastUBLow = 0;
		private double currentHigh = 0.0;
		private double currentLow = 0.0;
		
		private double volumeAtExtremeHigh = 0.0;
		private double volumeAtExtremeLow = 0.0;
		
		private bool LowTriggered  =true;
		private bool HighTriggered  =true;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name												= "Price Rejector Current";
				Calculate										= Calculate.OnBarClose;
				IsOverlay										= true;
				DisplayInDataBox						= true;
				DrawOnPricePanel						= true;
				DrawHorizontalGridLines			= true;
				DrawVerticalGridLines				= true;
				PaintPriceMarkers						= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive			= true;
				ShowPriceRejector						= true;
				ShowMaxDelta								= true; // max delta
				ShowExhaustion							= true; // filter the edges
				ShowUB											= true;
				//AlertColorBull							= Brushes.DodgerBlue;
				//AlertColorBear							= Brushes.Red;
				Minutes											= 30;
				//AreaBrush 									= System.Windows.Media.Brushes.DodgerBlue;
				Opacity											= 0.2;
				AString										= "smsAlert2.wav";
			}
			else if (State == State.Configure)
			{
				ClearOutputWindow();
				AddVolumetric("ES 06-21", BarsPeriodType.Minute, Minutes, VolumetricDeltaType.BidAsk, 1);
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
			  int currentBarRef = 0;
			
			  SharpDX.Direct2D1.SolidColorBrush frame2=new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,SharpDX.Color.Red,bp2);
			  SharpDX.Direct2D1.SolidColorBrush frame=new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,SharpDX.Color.DodgerBlue,bp2);
										
//			  if (ChartBars != null)
//			  {
//				    for (int barIndex = ChartBars.FromIndex; barIndex <= ChartBars.ToIndex; barIndex++)
//				    {
						//	if ( MinDelta.Count == MinDelta.Count ) {
//									int x = 0;
//									if (barIndex < MaxDelta.Count ) {
//											// max delta
//										//Print(" max delta");
//											int y=chartScale.GetYByValue(MaxDelta[barIndex ]) +priceAdjuster; 
//											x=chartControl.GetXByBarIndex(ChartBars,ChartBars.ToIndex)-(int)chartControl.BarWidth;
//											RenderTarget.DrawRectangle(new RectangleF(x,y,(int)chartControl.BarWidth*2,(float)(priceWidth)),frame,2);	
//									}
//									if (barIndex < MinDelta.Count ) {
//											// min delta
//										//Print("min delta");
//											int y2=chartScale.GetYByValue(MinDelta[barIndex])+priceAdjuster; 
//											RenderTarget.DrawRectangle(new RectangleF(x,y2,(int)chartControl.BarWidth*2,(float)(priceWidth)),frame2,2);	
//											//int x2=chartControl.GetXByBarIndex(ChartBars,barIndex) -  (int)chartControl.BarWidth; 
//									}
						//	}
							
//							/// Price Rejector at low - ' ': Error on calling 'OnRender' method on bar 83: You are accessing an index with a value that is invalid since it is out-of-range
//							//Print("Price Rejector at low"); 
//							if ( PriceRejector.Count > barIndex ) {
//								if ( PriceRejector[barIndex  ] == 1) { 
//										//Print("Barindex  " + barIndex + " CurrentBar " + CurrentBar + " currentbar ref " + currentBarRef );
//										int y3=chartScale.GetYByValue(RejectorSignals[barIndex ].low)+(priceAdjuster ); 
//										 x=chartControl.GetXByBarIndex(ChartBars,barIndex )-(int)chartControl.BarWidth; //SharpDX.Direct2D1.SolidColorBrush frame3=new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,SharpDX.Color.White,bp2);
//										SharpDX.Direct2D1.SolidColorBrush customDXBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.DodgerBlue); 
//									    customDXBrush.Opacity = (float)Opacity;
//										RenderTarget.FillRectangle(new RectangleF(x,y3,(int)chartControl.BarWidth*2,(float)(priceWidth*3)), customDXBrush);
//								}
//								//Print("Price Rejector at high");
//								if ( PriceRejector[barIndex ] ==  -1) { 
//										int y3=chartScale.GetYByValue(RejectorSignals[barIndex ].high)+(priceAdjuster - (priceWidth*2)); 
//										 x=chartControl.GetXByBarIndex(ChartBars,barIndex )-(int)chartControl.BarWidth;
//										SharpDX.Direct2D1.SolidColorBrush customDXBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, SharpDX.Color.Red); 
//									    customDXBrush.Opacity = (float)Opacity;
//									RenderTarget.FillRectangle(new RectangleF(x,y3,(int)chartControl.BarWidth*2,(float)(priceWidth*3)), customDXBrush);
//								}
//							}
//					    	currentBarRef +=1;
//					}
//				}
		}
		
		protected override void OnBarUpdate()
		{
			if(State == State.Historical) return;
			if (BarsInProgress == 1 ) {
				checkNewHighLow();
				PopulateList(debug: false) ;
				CleanArray() ;	
				if ( IsFirstTickOfBar) {
					volumeAtExtremeHigh = 0.0;
				}
			}
		}
		
		/// <summary>
		/// *********************************************************************************************************
		/// </summary>
		private void checkNewHighLow() {
			// remove all high - low text
				if ( High[0] > currentHigh) {
					Print("new high " + High[0]);
					RemoveDrawObject("Exhastionh"+CurrentBar);
					//RemoveDrawObject("Absorbtionh"+lastABhigh);
					RemoveDrawObject("RejectorHigh"+lastPRhigh);
					HighTriggered = false;
				
				}
				
				if ( Low[0] < currentLow ) {
					Print("new low " + Low[0]);
					RemoveDrawObject("Exhastion"+CurrentBar);
					//RemoveDrawObject("Absorbtion"+lastABLow);
					RemoveDrawObject("RejectorLow"+lastPRLow);
					LowTriggered = false;
				}
		}
		
		private void CleanArray() {
				int minC = MinDelta.Count;
				int maxC = MaxDelta.Count;
				if (minC > maxC) {
					MinDelta.RemoveAt(minC -1);
				}
				if (maxC > minC) {
					MaxDelta.RemoveAt(maxC -1);
				}
				currentHigh = High[0];
				currentLow = Low[0];
		}
		
		private void SetEdgeFilter() {
			if ( CurrentBar < 1 ) { 
						MinDelta.Add(0.0);
						MaxDelta.Add(0.0);
						PriceRejector.Add(0);
						RejectorSignal rejectorSignal = new RejectorSignal(0, Low[0], High[0]);
						RejectorSignals.Add(rejectorSignal);
						return; }
			    if ( ShowExhaustion )  {
				    if (High[0] > Bollinger1.Upper[0] || Low[0] <= Bollinger1.Lower[0])
					{
						GoodToTrade = true;
					} else {
						GoodToTrade = false;
					}
				}
		}
		
		private void PopulateList(bool debug) {
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
				if ( debug ) { Print("Bar: " + CurrentBar + "\t" + Time[0] + " \trange: " + NumPrices); }
				if ( debug ) { Print("fill array --------------------------------->");}
				for (int index = 0; index <= NumPrices; index++)
				{
				//	if ( debug ) { Print("bad loop? " + index );}
					double thisPrice = High[0] - (index * TickSize);
					//if ( debug ) { Print("bad loop? " + thisPrice );}
					double thisBid =  barsType.Volumes[CurrentBar].GetBidVolumeForPrice(thisPrice);
					double thisAsk =  barsType.Volumes[CurrentBar].GetAskVolumeForPrice(thisPrice);
					double thisDelta =  barsType.Volumes[CurrentBar].GetDeltaForPrice(thisPrice);
					BarSlice slice = new BarSlice(thisPrice, thisBid, thisAsk, thisDelta, 0.0, 0.0);
					FullBar.Add(slice);
					if ( debug ) { Print(  slice.priceLevel + " \tbid: " + slice.bidVolume + " \task: " + slice.askVolume + "\t delta: " + slice.delta);}
				}
				if ( debug ) { Print("exit array --------------------------------->");}
				int indexOfLast = FullBar.Count - 1;
				if ( indexOfLast == 0 ) { return;} 
				var maxDelta = FullBar.Max(d => d.delta);
				var minDelta = FullBar.Min(d => d.delta);
				if ( debug ) { Print("Min Delta: " + minDelta + " max: "  + maxDelta);}

				if ( ShowMaxDelta )
				foreach (var item in FullBar) {
					if (item.delta == maxDelta) {
						maxDeltaPrice = item.priceLevel;
						Draw.Text(this, "maxDeltaPrice", "Max", 0, maxDeltaPrice, Brushes.LightCyan);
						MaxDelta.Add(maxDeltaPrice);
					} 
					
					if (item.delta == minDelta) {
						minDeltaPrice = item.priceLevel;
						Draw.Text(this, "minDeltaPrice", "Max", 0, minDeltaPrice, Brushes.LightPink);
						MinDelta.Add(minDeltaPrice);
					} 
				}
	/*
		private double lastPRhigh = 0;
		private double lastABhigh = 0;
		private double lastUBhigh = 0;
		private double lastPRLow = 0;
		private double lastABLow = 0;
		private double lastUBLow = 0;
		*/
				/// Check for unfinished business
				if ( debug ) { Print("first bid: " + FullBar[0].bidVolume  + " \tlast offer: " + FullBar[indexOfLast ].askVolume );}
				PriceRejector.Add(0);	
				RejectorSignal rejectorSignal = new RejectorSignal(0, Low[0], High[0]);
				RejectorSignals.Add(rejectorSignal);
				
				//RejectorSignals.Add(new RejectorSignal(0, 0.0, 0.0));
				if (FullBar[indexOfLast].askVolume != 0.0 ) { 
					RemoveDrawObject("RejectorLow"+lastPRLow);
					RemoveDrawObject("Exhastion"+CurrentBar);
					Draw.Text(this, "Exhastion"+CurrentBar, "UB", 0, Low[0] - 1 * TickSize, Brushes.Yellow);
					
					// if vol lower then exhaustion
					if ( debug ) { Print("Comparing last ask " + FullBar[indexOfLast].askVolume + " to ask above" + FullBar[indexOfLast -1].askVolume );}
//					if (FullBar[indexOfLast].askVolume  < FullBar[indexOfLast -1].askVolume ) {
//						if( ShowUB && GoodToTrade) {
//							RemoveDrawObject("Exhastion"+lastABLow);
//							Draw.Text(this, "Exhastion"+Volume[0], "UBE          .", 0, Low[0] - 1 * TickSize, Brushes.Yellow);} 
//						    lastUBLow = Volume[0];
//					} else {
//						if( ShowUB && GoodToTrade) { 
//							// this double prints
//							RemoveDrawObject("Absorbtion"+lastABLow);
//							Draw.Text(this, "Absorbtion"+ Volume[0], ".     UBA", 0, Low[0] - 1 * TickSize, Brushes.Yellow);}
//						    lastABLow = Volume[0];
//					}
				} else {
					/// Check for Price rejector in completed auction
					// Volume gets lower in bar
					if (FullBar[indexOfLast].bidVolume  < FullBar[indexOfLast -1].bidVolume && FullBar[indexOfLast -1].bidVolume < FullBar[indexOfLast -2].bidVolume && FullBar[indexOfLast -2].bidVolume < FullBar[indexOfLast -3].bidVolume && 
						FullBar[indexOfLast].askVolume < FullBar[indexOfLast -1].askVolume &&  FullBar[indexOfLast -1].askVolume < FullBar[indexOfLast -2].askVolume && FullBar[indexOfLast -2].askVolume < FullBar[indexOfLast -3].askVolume) {
						// delta negative at 3 lowest levels
						if (FullBar[indexOfLast].delta <=0 && FullBar[indexOfLast - 1].delta <=0 && FullBar[indexOfLast - 2].delta <=0  ) { 
							// diagonal agression at lowest level
						//	/ low bid < low + 1 offer
							
			/// **********************     REJECCTOR LOW *********************
			
							if (FullBar[indexOfLast].bidVolume  < FullBar[indexOfLast - 1].askVolume )  { 
								if ( ShowPriceRejector ) { 
									RemoveDrawObject("Exhastion"+CurrentBar);
									RemoveDrawObject("RejectorLow"+lastPRLow);
									volumeAtExtremeLow = FullBar[indexOfLast].delta + FullBar[indexOfLast - 1].delta + FullBar[indexOfLast - 2].delta;
									string message = "Price Rejector " + volumeAtExtremeLow.ToString("N0");
									Draw.Text(this, "RejectorLow"+Volume[0], message, 0, Low[0] - 2 * TickSize, Brushes.DimGray); 
									PriceRejector[PriceRejector.Count - 1] = 1;
									lastPRLow = Volume[0];
									if ( !LowTriggered)
										sendAlert( message: "Price Rejector at Low", sound: AString);
									LowTriggered = true;
								}
							}
						}
					}
				}
				
				

				if ( debug ) { Print("Test bar low for UB or Price Rejector at High" );}
				// Test bar low for UB or Price Rejector at High
				if (FullBar[0].bidVolume != 0.0 ) { 
					
					RemoveDrawObject("RejectorHigh"+lastPRhigh);
					RemoveDrawObject("Exhastionh"+CurrentBar);
					Draw.Text(this, "Exhastionh"+ CurrentBar, "UB", 0, High[0] + 1 * TickSize, Brushes.Yellow);
					
					// if vol lower then exhaustion
					if ( debug ) { Print("Comparing last ask " + FullBar[indexOfLast].askVolume + " to ask above" + FullBar[indexOfLast -1].askVolume );}
//					if (FullBar[0].bidVolume  < FullBar[1].bidVolume ) {
//						if( ShowUB && GoodToTrade) {
//							RemoveDrawObject("Exhastionh"+lastUBhigh);
//							Draw.Text(this, "Exhastionh"+ Volume[0], "UBE          .", 0, High[0] + 1 * TickSize, Brushes.Yellow); }
//						    lastUBhigh = Volume[0];
						
//					} else {
//						if( ShowUB && GoodToTrade) { 
//							RemoveDrawObject("Absorbtionh"+lastABhigh);
//							Draw.Text(this, "Absorbtionh"+ Volume[0], ".        UBA", 0, High[0] + 1 * TickSize, Brushes.Yellow);}
//						    lastABhigh = Volume[0];
//					}
				} else {
					/// Check for Price rejector in completed auction
					// Volume gets lower in bar
					if (FullBar[0].bidVolume  < FullBar[1].bidVolume && FullBar[1].bidVolume < FullBar[2].bidVolume && FullBar[2].bidVolume < FullBar[3].bidVolume && 
						FullBar[0].askVolume < FullBar[1].askVolume &&  FullBar[1].askVolume < FullBar[2].askVolume && FullBar[2].askVolume < FullBar[3].askVolume) {
					//	// delta negative at 3 lowest levels
						if (FullBar[0].delta >=0 && FullBar[1].delta >=0 && FullBar[2].delta >=0  ) { 
							// diagonal agression at lowest level
							// low bid < low + 1 offer
												
			/// **********************     REJECCTOR HIGH      *********************
			
							
							if (FullBar[1].bidVolume  > FullBar[0].askVolume )  { 
								if ( ShowPriceRejector ) { 
									RemoveDrawObject("Exhastionh"+CurrentBar);
									RemoveDrawObject("RejectorHigh"+lastPRhigh);
									volumeAtExtremeHigh = FullBar[0].delta + FullBar[1].delta + FullBar[2].delta;
									//Print("Vol: " + volumeAtExtremeHigh);
									string message = "Price Rejector " + volumeAtExtremeHigh.ToString("N0");
									Draw.Text(this, "RejectorHigh"+Volume[0], message, 0, High[0] + 2 * TickSize, Brushes.Red);
									PriceRejector[PriceRejector.Count - 1] = -1;
									lastPRhigh = Volume[0];
									if ( !HighTriggered)
										sendAlert( message: "Price Rejector at High", sound: AString);
									HighTriggered = true;
								}	
							}
						}
					}
				}
				
				if ( volumeAtExtremeHigh > 0.0 ) {
					///Print("Seeking > "  + volumeAtExtremeHigh);
					/// // start adding volume of only levels traded past ???
					double excessVolume = FullBar[3].delta + FullBar[4].delta + FullBar[5].delta;
				}
				
	        }
	        catch{}
			if ( debug ) { Print("end of func" );}
		}
		
		private void sendAlert(string message, string sound ) {
			message += " " + Bars.Instrument.MasterInstrument.Name;
			Alert("myAlert"+CurrentBar, Priority.High, message, NinjaTrader.Core.Globals.InstallDir+@"\sounds\"+ sound,10, Brushes.Black, Brushes.Yellow);  
			if (CurrentBar < Count -2) return;
		}
		
		#region Properties
		[NinjaScriptProperty]
		[Display(Name="ShowPriceRejector", Order=1, GroupName="Parameters")]
		public bool ShowPriceRejector
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="Show Max Delta", Order=2, GroupName="Parameters")]
		public bool ShowMaxDelta
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="filter the edges", Order=3, GroupName="Parameters")]
		public bool ShowExhaustion
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name="ShowUB", Order=4, GroupName="Parameters")]
		public bool ShowUB
		{ get; set; }
		
		//  
		[NinjaScriptProperty]
		[Range(0.1, double.MaxValue)]
		[Display(Name="Opacity", Order=5, GroupName="Parameters")]
		public double Opacity
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Sound Name", Order=6, GroupName="Parameters")]
		public string AString
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
		public int Minutes
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
		private PriceRejectorCurrent[] cachePriceRejectorCurrent;
		public PriceRejectorCurrent PriceRejectorCurrent(bool showPriceRejector, bool showMaxDelta, bool showExhaustion, bool showUB, double opacity, string aString, int minutes)
		{
			return PriceRejectorCurrent(Input, showPriceRejector, showMaxDelta, showExhaustion, showUB, opacity, aString, minutes);
		}

		public PriceRejectorCurrent PriceRejectorCurrent(ISeries<double> input, bool showPriceRejector, bool showMaxDelta, bool showExhaustion, bool showUB, double opacity, string aString, int minutes)
		{
			if (cachePriceRejectorCurrent != null)
				for (int idx = 0; idx < cachePriceRejectorCurrent.Length; idx++)
					if (cachePriceRejectorCurrent[idx] != null && cachePriceRejectorCurrent[idx].ShowPriceRejector == showPriceRejector && cachePriceRejectorCurrent[idx].ShowMaxDelta == showMaxDelta && cachePriceRejectorCurrent[idx].ShowExhaustion == showExhaustion && cachePriceRejectorCurrent[idx].ShowUB == showUB && cachePriceRejectorCurrent[idx].Opacity == opacity && cachePriceRejectorCurrent[idx].AString == aString && cachePriceRejectorCurrent[idx].Minutes == minutes && cachePriceRejectorCurrent[idx].EqualsInput(input))
						return cachePriceRejectorCurrent[idx];
			return CacheIndicator<PriceRejectorCurrent>(new PriceRejectorCurrent(){ ShowPriceRejector = showPriceRejector, ShowMaxDelta = showMaxDelta, ShowExhaustion = showExhaustion, ShowUB = showUB, Opacity = opacity, AString = aString, Minutes = minutes }, input, ref cachePriceRejectorCurrent);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.PriceRejectorCurrent PriceRejectorCurrent(bool showPriceRejector, bool showMaxDelta, bool showExhaustion, bool showUB, double opacity, string aString, int minutes)
		{
			return indicator.PriceRejectorCurrent(Input, showPriceRejector, showMaxDelta, showExhaustion, showUB, opacity, aString, minutes);
		}

		public Indicators.PriceRejectorCurrent PriceRejectorCurrent(ISeries<double> input , bool showPriceRejector, bool showMaxDelta, bool showExhaustion, bool showUB, double opacity, string aString, int minutes)
		{
			return indicator.PriceRejectorCurrent(input, showPriceRejector, showMaxDelta, showExhaustion, showUB, opacity, aString, minutes);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.PriceRejectorCurrent PriceRejectorCurrent(bool showPriceRejector, bool showMaxDelta, bool showExhaustion, bool showUB, double opacity, string aString, int minutes)
		{
			return indicator.PriceRejectorCurrent(Input, showPriceRejector, showMaxDelta, showExhaustion, showUB, opacity, aString, minutes);
		}

		public Indicators.PriceRejectorCurrent PriceRejectorCurrent(ISeries<double> input , bool showPriceRejector, bool showMaxDelta, bool showExhaustion, bool showUB, double opacity, string aString, int minutes)
		{
			return indicator.PriceRejectorCurrent(input, showPriceRejector, showMaxDelta, showExhaustion, showUB, opacity, aString, minutes);
		}
	}
}

#endregion
