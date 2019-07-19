using System;
using System.Drawing;
using System.Linq;
using System.Dynamic;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Net.Sockets;
using PowerLanguage.Function;
using Newtonsoft.Json;

namespace PowerLanguage.Indicator{
	public class ForecastIndicator : IndicatorObject {
		public ForecastIndicator(object _ctx):base(_ctx)	{
			
			architecture = Architecture.LSTM;
			loss = Loss.MSE;
			optimizer = Optimizer.RMSProp;
			
			gpu = true;
			train = true;
			isTrained = false;
			
			trainingSize = 75;
			epochs = 10;
			scale = 100;
			
			fileName = "model1";
			
			momentum = 0.9;
			learningRate = 0.001;
			testingPart = 10;
			testingWeight = 50;
			
			bars = 5;
			prevTrain = 0;
			retrainInterval = 10;
		}
		private IPlotObject plot1;
		
		#region Enum Declaration
		public enum Architecture	{
			LSTM,
			GRU,
			BidirectionalLSTM,
			BidirectionalGRU
		};
		
		public enum Optimizer {
			RMSProp,
			SGD,
			Adam,
			Adagrad
		};
		
		public enum Loss   {
			MSE,
			R2
		};
		#endregion
		
		#region Class Definition

		// Parameters to be sent to the model for training
		public class trainParameters
		{
			public List<string> Data{ get; set;}
			public List<string> Time{get; set;}
			
			public string FileName {get; set;}

			public bool GPU {get; set;}
	
			public int Architecture {get; set;}
			public int Optimizer {get; set;}
			public int Loss{get; set;}
			public int Epochs {get; set;}
			public int Bars {get; set ;}
			public int Scale {get; set;}
			
			public double LearningRate {get; set;}
			public double Momentum {get; set;}
			public double TestingPart{get; set;}
			public double TestingWeight{get; set;}

		}

		// Parameters to be received from Trained model
		public class PredictionParameters
		{
			public List<double> Eval {get; set;}
			public List<double> Pred {get; set;}
		}
		
		#endregion
		
		#region Variables
			[Input]
			public Architecture architecture {get; set;} // RNN Architecture
			[Input]
			public Optimizer optimizer {get; set;} // Optimizer
			[Input]
			public Loss loss {get; set;}
				
			[Input]
			public bool gpu {get; set;} // Allow GPU Computations ?
			[Input]
			public bool train {get; set;} // Train ?
			
			public bool isTrained {get; set;}
			public int prevTrain {get; set;}
			
			//Train size must be greater than window_size = 60
			[Input]
			public int trainingSize {get; set;} // Train Size 
			[Input]
			public int epochs {get; set;}  // Epochs
			[Input]
			public int scale {get; set;} // Scale
				
			[Input]
			private string fileName {get; set;} // File Name to export model
			
			[Input]
			public double momentum {get; set;} // Momentum (for SGD)
			[Input]
			public double learningRate {get; set;} // Learning Rate 
			[Input]
			public double testingPart {get; set;} // Percentage of Train/Test Split
			[Input]
			public double testingWeight {get; set;} // Percentage of Train/Test Score Weights
	            	
			[Input]
			public int bars {get; set;}
			[Input]
			public int retrainInterval {get; set;}
				
			public TcpClient socket;
			public NetworkStream stream;
		#endregion
		
		
		protected override void Create() {
			// create variable objects, function objects, plot objects etc.
			plot1 = AddPlot(new PlotAttributes("", EPlotShapes.Line, Color.Red));
		}
		
		protected override void StartCalc() {

		}
		
		protected override void CalcBar(){
			// assign inputs
			if(Bars.Status != EBarState.Close)
				return;
			if(Bars.CurrentBar < trainingSize)
				return;
			
			if(!isTrained || (isTrained && prevTrain == retrainInterval))	{
				socket = new TcpClient();
				socket.Connect("localhost", 9090);
				stream = socket.GetStream();
				
				if(socket.Connected)	{
					Output.WriteLine("Connected to socket");
					
					List<string> closePrice = new List<string>();
					List<string> time = new List<string>();
					for(int i = 0; i < trainingSize; i++)
					{
						closePrice.Add(Bars.Close[i].ToString());
						time.Add(Bars.Time[i].ToString());
					}
					var jsonObject = new trainParameters();
				
					jsonObject.Data = closePrice;
					jsonObject.Time = time;
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
					Output.WriteLine("Sent : " + jsonString);
					
					isTrained = true;
					prevTrain = Bars.CurrentBar;
				}
				else
				{
					Output.WriteLine("Connection Failed");
				}
			}
			// Receiving data after training from the server 
			else if(socket.Connected)	{
				byte[] data = new Byte[2*256];
				string response = "";
				Int32 bytes = stream.Read(data, 0, data.Length);
				if(bytes > 0)
				response += Encoding.UTF8.GetString(data,0,bytes);

				if(response != "")
			        { 
					Output.WriteLine("Received : " + response);
					var jsonObject = new PredictionParameters();

					// Deserializing JSON data 
					jsonObject = JsonConvert.DeserializeObject<PredictionParameters>(response);

					// Plotting the predictions on the chart
					/*for (int i=-1;i>=-1*bars;i--)
					{
						double ypred = double.Parse(jsonObject.Pred[(-1*i)-1].ToString());
						DrawDot("Prediction " + i.ToString(), true, i, ypred, Color.Cyan);

					} */
							
					// closing the socket
					stream.Close();
					socket.Close();
				}
				else
					Output.WriteLine("Not Received");
			}
			else
			{
				Output.WriteLine("Socket Disconnected");
			}
		}
	}
}