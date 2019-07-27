#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Windows.Media;
using NinjaTrader.NinjaScript.DrawingTools;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Dynamic;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class RNNProject_NT8_Indi : Indicator
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
			private bool isReceived = false;
			
		    public TcpClient socket;                  // Creating client for connection via socket
			public NetworkStream stream;              // NetworkStream variable to read and write data
		#endregion
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Indicator to Predict Time Series data using a model trained by RNN";
				Name										= "RNNProject_NT8_Indi";
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
				
				retrain                                     = true;
			}
			else if (State == State.Configure)
			{
			}
		}
		
		protected void receivePred()
		{
			if (DEBUG) Print("Can Read : " + stream.CanRead.ToString());
			if (DEBUG) Print("Is Data Available : " + stream.DataAvailable.ToString());

			 if(stream.DataAvailable)
			 {
				byte[] data     = new Byte[2*256];
				string response = string.Empty;
				Int32 bytes     = stream.Read(data, 0, data.Length);
				response        = Encoding.UTF8.GetString(data,0,bytes);

				if(response != string.Empty)
			        { 
					if (DEBUG) 	Print("Received : " + response);

					Print("Data Received Successfully!");
					Draw.TextFixed(this, "Chart Message", "Predictions Received!\nPlotting on Chart..." , TextPosition.TopRight);
					dynamic jsonObject = new ExpandoObject();						
					jsonObject         = JsonConvert.DeserializeObject(response);

					// Plotting the predictions on  the chart
					for (int i=-1; i>=-1*bars; i--)
					{
						double ypred = double.Parse(jsonObject.Pred[(-1*i)-1].ToString());
						Draw.Dot(this, "Prediction " + i.ToString(), true, i, ypred, Brushes.Aqua);
						Draw.TextFixed(this, "Chart Message", "" , TextPosition.TopRight);
					} 

					stream.Close();
				        socket.Close();
				}
			 }
			 else
			 {
				Print("Please Wait... Loading Predictions...");
				Draw.TextFixed(this, "Chart Message", "Loading Predictions\nPlease Wait..." , TextPosition.TopRight);
			 }
		}

		protected override void OnBarUpdate()
		{
			// For Running on Real Time Data
			if (State == State.Historical)
				return;
						
			if ( train == true)
			{
				// Collect Enough Data
				if (CurrentBar < trainingSize)
				{
					Draw.TextFixed(this, "Chart Message", "Not Enough data for Training!" , TextPosition.TopRight);
					return;
				}
				
				// Number of bars elapsed since previous Training
			        int interval = CurrentBar - prevTrain;
				
				if (!isTrained || (retrain && interval == retrainInterval))
				{
					// Establishing connection
					try 
					{
						socket = new TcpClient();
						socket.Connect("localhost", 9090);          // Connecting to python server on localhost
						stream = socket.GetStream();                // Creating stream to read and write data
					}
					catch (ArgumentNullException e)
					{
						Print(Time[0].ToString()+ " Exception Occured! The hostname parameter is null. "+ e.ToString());
					}
					catch (ArgumentOutOfRangeException e)
					{
						Print(Time[0].ToString()+ " Exception Occured! The port parameter is not between MinPort and MaxPort."+ e.ToString());
					}
					catch (SocketException e)
					{
						Print(Time[0].ToString()+ " Exception Occured! "+ e.ToString());
					}
					catch (ObjectDisposedException e)
					{
						Print(Time[0].ToString()+ " Exception Occured! TcpClient is closed. "+ e.ToString());
					}

					if (socket.Connected)
					{
						Print("connected to localhost : 9090");
						Draw.TextFixed(this, "Chart Message", "Connected!" , TextPosition.TopRight);
						// Collecting close Price and Dates data
						List<string> closePrice = new List<string>();
						List<string> time = new List<string>();
						for (int index = 0; index < trainingSize; index++) 
						{
							closePrice.Add(Close[index].ToString() );	
							time.Add(Time[index].ToString());
						}

						closePrice.Reverse();
						time.Reverse();
						
						// Creating dynamic object to store model parameters
						dynamic jsonObject = new ExpandoObject();				
						
						jsonObject.Data          = closePrice;
						jsonObject.Time          = time;
						jsonObject.FileName      = fileName;
						jsonObject.Train         = train;
						jsonObject.GPU           = gpu;
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
	         
						if (stream.CanWrite)
						{
							stream.Write(data, 0, data.Length);		         
							
							if (DEBUG)   
								Print("Sent : " + jsonString);

							Print("Data Sent Successfully!");
							Draw.TextFixed(this, "Chart Message", "Data Sent..." , TextPosition.TopRight);
							isTrained = true;
							prevTrain = CurrentBar;	
						}
						else
						{
						    Print("Data cannot be sent to the stream!");
							stream.Close();
							socket.Close();
							return;
						}
					}
					else
					{
						Print("Connection Failed!");
						Draw.TextFixed(this, "Chart Message", "Connection Failed!" , TextPosition.TopRight);
					}
					
					
				}      
				
				// Receiving result from trained model
				 if(socket.Connected)
				 {
					receivePred();
				 }				
				
			}           // end of function implementing prediction with real time training
			
			// When Train is False
			else if(!isReceived)
			{
				// Establishing connection				
				try 
				{
					socket = new TcpClient();
					socket.Connect("localhost", 9090);          // Connecting to python server on localhost
					stream = socket.GetStream();                // Creating stream to read and write data
				}
				catch (ArgumentNullException e)
				{
					Print(Time[0].ToString()+ " Exception Occured! The hostname parameter is null. "+ e.ToString());
				}
				catch (ArgumentOutOfRangeException e)
				{
					Print(Time[0].ToString()+ " Exception Occured! The port parameter is not between MinPort and MaxPort."+ e.ToString());
				}
				catch (SocketException e)
				{
					Print(Time[0].ToString()+ " Exception Occured! "+ e.ToString());
				}
				catch (ObjectDisposedException e)
				{
					Print(Time[0].ToString()+ " Exception Occured! TcpClient is closed. "+ e.ToString());
				}
				
				if (socket.Connected)
				{
					Print("Connected to localhost : 9090");
					Draw.TextFixed(this, "Chart Message", "Connected!" , TextPosition.TopRight);
					
					// Creating dynamic object to store model parameters
					dynamic jsonObject = new ExpandoObject();				

					jsonObject.FileName      = fileName;
					jsonObject.Train         = train;
					jsonObject.Bars          = bars;
						
					string jsonString   = JsonConvert.SerializeObject(jsonObject);
					Byte[] data         = Encoding.UTF8.GetBytes(jsonString);
		     
					if (stream.CanWrite)
					{
						stream.Write(data, 0, data.Length);	
										
						Print("Data Sent Successfully!");
						Draw.TextFixed(this, "Chart Message", "Data Sent..." , TextPosition.TopRight);
					}
					else
					{
						Print("Data cannot be sent to the stream!");
						stream.Close();
						socket.Close();
						return;
					}
						
					if (DEBUG) Print("Can Read : " + stream.CanRead.ToString());
					if (DEBUG) Print("Is Data Available : " + stream.DataAvailable.ToString());

					if(stream.CanRead)
					{
						byte[] recData     = new Byte[256];
						string response = string.Empty;
						Int32 bytes     = stream.Read(recData, 0, recData.Length);
						response        = Encoding.UTF8.GetString(recData,0,bytes);

						if(response != string.Empty)
						{ 
							if (DEBUG) 	Print("Received : " + response);
								
							Print("Data Received Successfully!");
							Draw.TextFixed(this, "Chart Message", "Predictions Received!\nPlotting on Chart..." , TextPosition.TopRight);
							dynamic jsonObj = new ExpandoObject();						
							jsonObj         = JsonConvert.DeserializeObject(response);

							// Plotting the predictions on  the chart
							for (int i=-1; i>=-1*bars; i--)
							{
								double ypred = double.Parse(jsonObj.Pred[(-1*i)-1].ToString());
								Draw.Dot(this, "Prediction " + i.ToString(), true, i, ypred, Brushes.Aqua);
								Draw.TextFixed(this, "Chart Message", "" , TextPosition.TopRight);
							} 

							stream.Close();
							socket.Close();
						}
					}
					else
					{
						Print("Prediction Data Not Available!\nPlease Train the Model...");
						Draw.TextFixed(this, "Chart Message", "Predictions Not Available!\nPlease Train the Model..." , TextPosition.TopRight);
					}
						
					isReceived  = true;
						
				}
				else
				{
					Print("Connection Failed!");
					Draw.TextFixed(this, "Chart Message", "Connection Failed!" , TextPosition.TopRight);
				}

			}          // end of function implementing prediction without training

			
		}              // end of OnBarUpdate()
			
		
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
			
			[Display(Name = "FileName", Order = 9, Description="Name of file to store Model")]
			public string m_fileName
			{
				get {return fileName;}
				set {fileName = value;}
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
						
			[Display(Name = "Retrain", Order = 14, Description="Whether Retraining should be done or Not")]
			public bool retrain
			{
				get; set;
			}
			
			[Display(Name = "Retrain Interval(in bars)", Order = 15, Description="Interval after which to automatically retrain")]
			public int m_retrainInterval
			{
				get {return retrainInterval;}
				set{retrainInterval = value;}
			}
		#endregion
		
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RNNProject_NT8_Indi[] cacheRNNProject_NT8_Indi;
		public RNNProject_NT8_Indi RNNProject_NT8_Indi()
		{
			return RNNProject_NT8_Indi(Input);
		}

		public RNNProject_NT8_Indi RNNProject_NT8_Indi(ISeries<double> input)
		{
			if (cacheRNNProject_NT8_Indi != null)
				for (int idx = 0; idx < cacheRNNProject_NT8_Indi.Length; idx++)
					if (cacheRNNProject_NT8_Indi[idx] != null &&  cacheRNNProject_NT8_Indi[idx].EqualsInput(input))
						return cacheRNNProject_NT8_Indi[idx];
			return CacheIndicator<RNNProject_NT8_Indi>(new RNNProject_NT8_Indi(), input, ref cacheRNNProject_NT8_Indi);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RNNProject_NT8_Indi RNNProject_NT8_Indi()
		{
			return indicator.RNNProject_NT8_Indi(Input);
		}

		public Indicators.RNNProject_NT8_Indi RNNProject_NT8_Indi(ISeries<double> input )
		{
			return indicator.RNNProject_NT8_Indi(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RNNProject_NT8_Indi RNNProject_NT8_Indi()
		{
			return indicator.RNNProject_NT8_Indi(Input);
		}

		public Indicators.RNNProject_NT8_Indi RNNProject_NT8_Indi(ISeries<double> input )
		{
			return indicator.RNNProject_NT8_Indi(input);
		}
	}
}

#endregion
