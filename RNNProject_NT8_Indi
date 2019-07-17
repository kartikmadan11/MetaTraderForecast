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
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Dynamic;
using System.Runtime.Serialization;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class __HPCS_RNNProject_NT8_Indi_V01_WE : Indicator
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"RNN Project predictor";
				Name										= "__HPCS_RNNProject_NT8_Indi_V01_WE";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
			}
			else if (State == State.Configure)
			{
				
			}
			
			else if(State == State.Terminated)
			{
				//stream.Close();
				//socket.Close();
			}
			
		}
		
	    #region Enum Declaration
		   public enum Optimizer {
			  RMSProp,
			  SGD,
			  Adam,
			  Adagrad
			};
			  
			public enum Architecture {
			  LSTM,
			  GRU,
			  BidirectionalLSTM,
			  BidirectionalGRU
			};
			 
			public enum Loss   {
			   MSE,
			   R2
			};
		#endregion
		
		#region Private Variables
			private Architecture architecture = Architecture.LSTM; // RNN Architecture
		    private Optimizer optimizer  = Optimizer.RMSProp; // Optimizer
		    private Loss loss = Loss.MSE;
			
			private bool gpu = true; // Allow GPU Computations ?
			private bool train = true; // Train ?

			private bool isTrained = false;
			//Train size must be greater than window_size = 60
			private int trainingSize = 500 ; // Train Size 
			private int epochs = 10;  // Epochs
			private int scale = 100; // Scale
						
			private string fileName = "model1"; // File Name to export model

			private double momentum = 0.9; // Momentum (for SGD)
			private double learningRate = 0.001; // Learning Rate 
			private double testingPart = 10; // Percentage of Train/Test Split
			private double testingWeight = 50; // Percentage of Train/Test Score Weights
            				
			private int bars = 5;
			
		    public TcpClient socket;
			public NetworkStream stream;
		#endregion

		protected override void OnBarUpdate()
		{
			//Print(State.ToString());
			// For Running on Real Time Data
			if(State == State.Historical)
				return;
			
			// Collect Enough Data
			if (CurrentBar < trainingSize)
				return;

			// Training the model
			if (!isTrained)
			{
				// Establishing connection				
				socket = new TcpClient();
				socket.Connect("localhost", 9090);
				stream = socket.GetStream();

				if (socket.Connected)
	            {
	                Print("connected!");
						
					// Collecting close Price and Dates data
					List<string> closePrice = new List<string>();
					List<string> time = new List<string>();
					for (int index = 0; index < trainingSize; index++) 
				    {
						 closePrice.Add(Close[index].ToString() );	
						 time.Add(Time[index].ToString());
				    }
					
					
					// Creating dynamic object to store model parameters
					dynamic jsonObject = new ExpandoObject();				
					
					jsonObject.Data = closePrice;
					jsonObject.Time = time;
					jsonObject.FileName = fileName;
					jsonObject.GPU = gpu;
					jsonObject.Architecture = (int)architecture;
					jsonObject.Optimizer = (int)optimizer;
					jsonObject.Loss = (int)loss;
					jsonObject.LearningRate = learningRate;
					jsonObject.Epochs = epochs;
					jsonObject.Scale = scale;
					jsonObject.Momentum = momentum;
					jsonObject.TestingPart = testingPart;
					jsonObject.TestingWeight = testingWeight;
					jsonObject.Bars = bars;
					
					string jsonString = JsonConvert.SerializeObject(jsonObject);
					Byte[] data = Encoding.UTF8.GetBytes(jsonString);
         
					stream.Write(data, 0, data.Length);		         
					Print("Sent : " + jsonString);

					isTrained = true;

				}	
				
				else
			     {
				   Print("connection failed!");
			     }
			}
			 // Receiving result from trained model
			 else if(socket.Connected)
				{
					byte[] data = new Byte[2*256];
		            string response = string.Empty;
		            Int32 bytes = stream.Read(data, 0, data.Length);
		            response = Encoding.UTF8.GetString(data,0,bytes);

					if(response != string.Empty)
		            { 
						Print("Received : " + response);
						dynamic jsonObject = new ExpandoObject();
						
						jsonObject = JsonConvert.DeserializeObject(response);

						// Plotting the predictions on  the chart
						for (int i=-1;i>=-5;i--)
						{
							double ypred = double.Parse(jsonObject.Pred[(-1*i)-1].ToString());
							Draw.Dot(this, "Prediction " + i.ToString(), true, i, ypred, Brushes.Aqua);
						} 
	
						stream.Close();
				        socket.Close();
					}
					else
						Print("Not Received");
					
				}	
				
				else
					Print("Socket Disconnected! ");
								
			}
		
	
		#region Properties
			[Display(Name = "Architecture", Order = 0, Description="")]
			public Architecture m_architecture
			{
				get { return architecture; }
			    set { architecture = value; }
			}
			
			[Display(Name = "Optimizer", Order = 1, Description="")]
			public Optimizer m_optimizer
			{
				get { return optimizer; }
			    set { optimizer = value; }
			}
			
			[Display(Name = "Loss", Order = 2, Description="")]
			public Loss m_loss
			{
				get { return loss; }
			    set { loss = value; }
			}
			
			[Display(Name = "gpu",  Order = 3,Description="")]
			public bool m_gpu
			{
				get {return gpu;}
				set{gpu = value;}
			}
			
			[Display(Name = "train",  Order = 4, Description="")]
			public bool m_train
			{
				get {return train;}
				set{train = value;}
			}
			
			[Display(Name = "trainingSize",  Order = 5, Description="")]
			public int m_trainingSize
			{
				get {return trainingSize;}
				set{ trainingSize = value;}
			}
			
			[Display(Name = "epochs",  Order = 6, Description="")]
			public int m_epochs
			{
				get {return epochs;}
				set{epochs = value;}
			}
			
			[Display(Name = "scale", Order = 7, Description="")]
			public int m_scale
			{
				get {return scale;}
				set{scale = value;}
			}
			
			[Display(Name = "bars", Order = 8, Description="")]
			public int m_bars
			{
				get {return bars;}
				set{bars = value;}
			}
			
			[Display(Name = "momentum", Order = 9, Description="")]
			public double m_momentum
			{
				get {return momentum;}
				set{momentum = value;}
			}
			
			[Display(Name = "learningRate",  Order = 10, Description="")]
			public double m_learningRate
			{
				get {return learningRate;}
				set{learningRate = value;}
			}
			
			[Display(Name = "testingPart",  Order = 11, Description="")]
			public double m_testingPart
			{
				get {return testingPart ;}
				set{ testingPart = value;}
			}
			
			[Display(Name = "testingWeight",  Order = 12,Description="")]
			public double m_testingWeight
			{
				get {return testingWeight;}
				set{testingWeight = value;}
			}
			
			[Display(Name = "fileName", Order = 13, Description="")]
			public string m_fileName
			{
				get {return fileName;}
				set {fileName = value;}
			}
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private __HPCS_RNNProject_NT8_Indi_V01_WE[] cache__HPCS_RNNProject_NT8_Indi_V01_WE;
		public __HPCS_RNNProject_NT8_Indi_V01_WE __HPCS_RNNProject_NT8_Indi_V01_WE()
		{
			return __HPCS_RNNProject_NT8_Indi_V01_WE(Input);
		}

		public __HPCS_RNNProject_NT8_Indi_V01_WE __HPCS_RNNProject_NT8_Indi_V01_WE(ISeries<double> input)
		{
			if (cache__HPCS_RNNProject_NT8_Indi_V01_WE != null)
				for (int idx = 0; idx < cache__HPCS_RNNProject_NT8_Indi_V01_WE.Length; idx++)
					if (cache__HPCS_RNNProject_NT8_Indi_V01_WE[idx] != null &&  cache__HPCS_RNNProject_NT8_Indi_V01_WE[idx].EqualsInput(input))
						return cache__HPCS_RNNProject_NT8_Indi_V01_WE[idx];
			return CacheIndicator<__HPCS_RNNProject_NT8_Indi_V01_WE>(new __HPCS_RNNProject_NT8_Indi_V01_WE(), input, ref cache__HPCS_RNNProject_NT8_Indi_V01_WE);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.__HPCS_RNNProject_NT8_Indi_V01_WE __HPCS_RNNProject_NT8_Indi_V01_WE()
		{
			return indicator.__HPCS_RNNProject_NT8_Indi_V01_WE(Input);
		}

		public Indicators.__HPCS_RNNProject_NT8_Indi_V01_WE __HPCS_RNNProject_NT8_Indi_V01_WE(ISeries<double> input )
		{
			return indicator.__HPCS_RNNProject_NT8_Indi_V01_WE(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.__HPCS_RNNProject_NT8_Indi_V01_WE __HPCS_RNNProject_NT8_Indi_V01_WE()
		{
			return indicator.__HPCS_RNNProject_NT8_Indi_V01_WE(Input);
		}

		public Indicators.__HPCS_RNNProject_NT8_Indi_V01_WE __HPCS_RNNProject_NT8_Indi_V01_WE(ISeries<double> input )
		{
			return indicator.__HPCS_RNNProject_NT8_Indi_V01_WE(input);
		}
	}
}

#endregion
