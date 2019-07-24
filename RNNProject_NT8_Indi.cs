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
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class __RNNProject_NT8_Indi_V02_WE : Indicator
	{
		#region Enum Declaration
			public enum Optimizer 
			{
				RMSProp,
				SGD,
				Adam,
				Adagrad
			};

			public enum Architecture 
			{
				LSTM,
				GRU,
				BidirectionalLSTM,
				BidirectionalGRU
			};

			public enum Loss   
			{
				MSE,
				R2
			};
		#endregion
		
		#region Private Variables
		
		private Architecture architecture = Architecture.LSTM;   // RNN Architecture
		private Optimizer optimizer  = Optimizer.RMSProp;        // Optimizer
		private Loss loss = Loss.MSE;                            // Loss

		private bool gpu = true;                  // Allow GPU Computations ?
		private bool train = true;                // Allow Train ?

		//Train size must be greater than window_size = 60
		private int trainingSize = 500 ;          // Train Size 
		private int epochs = 10;                  // Epochs
		private int scale = 100;                  // Scale

		private string fileName = "model1";       // File Name to export model

		private double momentum = 0.9;            // Momentum (for SGD)
		private double learningRate = 0.001;      // Learning Rate 
		private double testingPart = 10;          // Percentage of Train/Test Split
		private double testingWeight = 50;        // Percentage of Train/Test Score Weights

		private int bars = 5;                     // Number of future bars to predict
		private int retrainInterval = 10;         // Interval (in bars) after which to automatically retrain
		private int prevTrain = 0;                // Index of bar on which model was previously trained
		private bool isTrained = false;           // Varibale to check if the model has been trained or not
		private bool DEBUG = true;

		public TcpClient socket;                  // Creating client for connection via socket
		public NetworkStream stream;              // NetworkStream variable to read and write data

		#endregion
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description							= @"Includes retraining od data";
				Name								    = "__RNNProject_NT8_Indi_V02_WE";
				Calculate							  = Calculate.OnBarClose;
				IsOverlay							  = false;
				DisplayInDataBox						= true;
				DrawOnPricePanel						= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers						= true;
				ScaleJustification						= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
			
      }
			else if (State == State.Configure)
			{
			}
		}

		protected override void OnBarUpdate()
		{
			if (DEBUG) 
			{  
				Print(State.ToString()); 
			} 
			
			// For Running on Real Time Data
			if(State == State.Historical)
				return;
			
			// Collect Enough Data
			if (CurrentBar < trainingSize)
				return;
			
			// Number of bars elapsed since previous Training
			int interval = CurrentBar - prevTrain;
			
			// Training the model
			if (!isTrained || (isTrained && interval == retrainInterval))
			{
				// Establishing connection				
				socket = new TcpClient();
				socket.Connect("localhost", 9090);          // Connecting to python server on localhost
				stream = socket.GetStream();                // Creating stream to read and write data

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

					jsonObject.Data          = closePrice;
					jsonObject.Time          = time;
					jsonObject.FileName      = fileName;
					jsonObject.GPU           = gpu;
					jsonObject.Train 		 = train;
					jsonObject.Architecture  = (int)architecture;
					jsonObject.Optimizer     = (int)optimizer;
					jsonObject.Loss          = (int)loss;
					jsonObject.LearningRate  = learningRate;
					jsonObject.Epochs        = epochs;
					jsonObject.Scale         = scale;
					jsonObject.Momentum      = momentum;
					jsonObject.TestingPart   = testingPart;
					jsonObject.TestingWeight = testingWeight;
					jsonObject.Bars          = bars;
					
					string jsonString   = JsonConvert.SerializeObject(jsonObject);
					Byte[] data         = Encoding.UTF8.GetBytes(jsonString);
         
					stream.Write(data, 0, data.Length);		         
					
					if (DEBUG)   
						Print("Sent : " + jsonString);

					isTrained = true;
					prevTrain = CurrentBar;
				}	
				
				else
			  {
				    Print("connection failed!");
			   }
			 }
			 // Receiving result from trained model
			 if(socket.Connected)
			 {
				  if (DEBUG) Print("Can Read : " + stream.CanRead.ToString());
			  	if (DEBUG) Print("Is Data Available : " + stream.DataAvailable.ToString());
				 
				 if(stream.DataAvailable)
				 {
					  //socket.ReceiveTimeout = 20000;
					  byte[] data     = new Byte[2*256];
		        string response = string.Empty;
		     		Int32 bytes     = stream.Read(data, 0, data.Length);
		     		response        = Encoding.UTF8.GetString(data,0,bytes);

					if(response != string.Empty)
		            		{ 
						if (DEBUG) 
							Print("Received : " + response);
						
						dynamic jsonObject = new ExpandoObject();						
						jsonObject         = JsonConvert.DeserializeObject(response);

						// Plotting the predictions on  the chart
						for (int i=-1; i>=-1*bars; i--)
						{
							double ypred = double.Parse(jsonObject.Pred[(-1*i)-1].ToString());
							Draw.Dot(this, "Prediction " + i.ToString(), true, i, ypred, Brushes.Aqua);
						} 
	
						stream.Close();
           	socket.Close();
					}
				 }
				 else
					Print("Prediction Data Not Available!");
					
			}				
			else
				Print("Socket Disconnected! ");
				
		}             // end of OnBarUpdate() method
		
		#region Properties
			[Display(Name = "Architecture", Order = 0, Description="Architecture of the Training Model")]
			public Architecture m_architecture
			{
				get { return architecture; }
			    set { architecture = value; }
			}
			
			[Display(Name = "Optimizer", Order = 1, Description="Optimizer to be Used")]
			public Optimizer m_optimizer
			{
				get { return optimizer; }
			    set { optimizer = value; }
			}

			[Display(Name = "Loss", Order = 2, Description="Loss Function")]
			public Loss m_loss
			{
				get { return loss; }
			    set { loss = value; }
			}

			[Display(Name = "GPU",  Order = 3,Description="If GPU is enabled")]
			public bool m_gpu
			{
				get {return gpu;}
				set{gpu = value;}
			}

			[Display(Name = "Train",  Order = 4, Description="If training is enabled")]
			public bool m_train
			{
				get {return train;}
				set{train = value;}
			}

			[Display(Name = "Training Size",  Order = 5, Description="Size of data to be sent for training")]
			public int m_trainingSize
			{
				get {return trainingSize;}
				set{ trainingSize = value;}
			}

			[Display(Name = "Epochs",  Order = 6, Description="Epochs")]
			public int m_epochs
			{
				get {return epochs;}
				set{epochs = value;}
			}

			[Display(Name = "Scale", Order = 7, Description="Scaling Parameter")]
			public int m_scale
			{
				get {return scale;}
				set{scale = value;}
			}

			[Display(Name = "Bars to Predict", Order = 8, Description="Number of future bars to predict")]
			public int m_bars
			{
				get {return bars;}
				set{bars = value;}
			}

			[Display(Name = "Retrain Interval(in bars)", Order = 9, Description="Interval after which to automatically retrain")]
			public int m_retrainInterval
			{
				get {return retrainInterval;}
				set{retrainInterval = value;}
			}
			

			[Display(Name = "Momentum", Order = 10, Description="Momentum")]
			public double m_momentum
			{
				get {return momentum;}
				set{momentum = value;}
			}

			[Display(Name = "Learning Rate",  Order = 11, Description="Learning Rate for the model")]
			public double m_learningRate
			{
				get {return learningRate;}
				set{learningRate = value;}
			}
			
      [Display(Name = "Testing Part",  Order = 12, Description="Train/Test data split (in percentage)")]
			public double m_testingPart
			{
				get {return testingPart ;}
				set{ testingPart = value;}
			}

			[Display(Name = "Testing Weight",  Order = 13,Description="Train/Test score(in percentage)")]
			public double m_testingWeight
			{
				get {return testingWeight;}
				set{testingWeight = value;}
			}

			[Display(Name = "fileName", Order = 14, Description="Name of file to store Model")]
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
		private __RNNProject_NT8_Indi_V02_WE[] cache__RNNProject_NT8_Indi_V02_WE;
		public __RNNProject_NT8_Indi_V02_WE __RNNProject_NT8_Indi_V02_WE()
		{
			return __RNNProject_NT8_Indi_V02_WE(Input);
		}

		public __RNNProject_NT8_Indi_V02_WE __RNNProject_NT8_Indi_V02_WE(ISeries<double> input)
		{
			if (cache__RNNProject_NT8_Indi_V02_WE != null)
				for (int idx = 0; idx < cache__RNNProject_NT8_Indi_V02_WE.Length; idx++)
					if (cache__RNNProject_NT8_Indi_V02_WE[idx] != null &&  cache__RNNProject_NT8_Indi_V02_WE[idx].EqualsInput(input))
						return cache__RNNProject_NT8_Indi_V02_WE[idx];
			return CacheIndicator<__RNNProject_NT8_Indi_V02_WE>(new __RNNProject_NT8_Indi_V02_WE(), input, ref cache__RNNProject_NT8_Indi_V02_WE);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.__RNNProject_NT8_Indi_V02_WE __RNNProject_NT8_Indi_V02_WE()
		{
			return indicator.__RNNProject_NT8_Indi_V02_WE(Input);
		}

		public Indicators.__RNNProject_NT8_Indi_V02_WE __RNNProject_NT8_Indi_V02_WE(ISeries<double> input )
		{
			return indicator.__RNNProject_NT8_Indi_V02_WE(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.__RNNProject_NT8_Indi_V02_WE __RNNProject_NT8_Indi_V02_WE()
		{
			return indicator.__RNNProject_NT8_Indi_V02_WE(Input);
		}

		public Indicators.__RNNProject_NT8_Indi_V02_WE __RNNProject_NT8_Indi_V02_WE(ISeries<double> input )
		{
			return indicator.__RNNProject_NT8_Indi_V02_WE(input);
		}
	}
}

#endregion
