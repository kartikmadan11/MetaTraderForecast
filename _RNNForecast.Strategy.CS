using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Text;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Dynamic;

namespace PowerLanguage.Strategy {
	public class _RNNForecast : SignalObject {
		
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
		public class TrainParameters
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
		
		public _RNNForecast(object _ctx):base(_ctx){
			architecture = Architecture.LSTM;
			loss = Loss.MSE;
			optimizer = Optimizer.RMSProp;
			
			gpu = true;
			train = true;
			isTrained = false;
			
			trainingSize = 50;
			epochs = 5;
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
		
		protected override void Create() {
			// create variable objects, function objects, order objects etc.
			
		}
		protected override void StartCalc() {
			// assign inputs 

		}
		protected override void CalcBar(){
			// strategy logic 
			

			
			if(Bars.CurrentBar <= trainingSize)
			{
				Output.WriteLine(Bars.CurrentBar.ToString() + " : ");
				return;
			}
			
			if(Bars.Status == EBarState.Close && !isTrained)	
			{
				Output.WriteLine(Bars.CurrentBar.ToString() + " : ");
				
				// Establishing connection				
				socket = new TcpClient();
				socket.Connect("localhost", 9090);          // Connecting to python server on localhost
				stream = socket.GetStream();                // Creating stream to read and write data

				if (socket.Connected)
	            {
	                Output.WriteLine("connected!");
						
					// Collecting close Price and Dates data
					List<string> closePrice = new List<string>();
					List<string> time = new List<string>();
					for (int index = 0; index < trainingSize; index++) 
				    {
						 closePrice.Add(Bars.Close[index].ToString() );	
						 time.Add(Bars.Time[index].ToString());
				    }
					
					
					// Creating dynamic object to store model parameters
					var jsonObject = new TrainParameters();				
					
					jsonObject.Data          = closePrice;
					jsonObject.Time          = time;
					jsonObject.FileName      = fileName;
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
         
					stream.Write(data, 0, data.Length);		         
					
					 
						
				    //Output.WriteLine("Sent : " + jsonString);

					Output.WriteLine("Sent!" );
					isTrained = true;
					//prevTrain = Bars.CurrentBar;
				}	
				
				else
			     {
				   Output.WriteLine("connection failed!");
			     }
					
			}
			
			else if(Bars.Status == EBarState.Close && isTrained && socket.Connected)
			{
				if(stream.DataAvailable)
				 {
					//socket.ReceiveTimeout = 20000;
					byte[] data     = new Byte[2*256];
		            string response = string.Empty;
		            Int32 bytes     = stream.Read(data, 0, data.Length);
		            response        = Encoding.UTF8.GetString(data,0,bytes);

					if(response != string.Empty)
		            { 
						//if (DEBUG) 
							//Output.WriteLine("Received : " + response);
						Output.WriteLine("Received!");
						var jsonObject = new PredictionParameters();						
						jsonObject         = JsonConvert.DeserializeObject<PredictionParameters>(response);

						// Plotting the predictions on  the chart
						/*for (int i=-1; i>=-1*bars; i--)
						{
							double ypred = double.Parse(jsonObject.Pred[(-1*i)-1].ToString());
							Draw.Dot(this, "Prediction " + i.ToString(), true, i, ypred, Brushes.Aqua);
						} */
	
						stream.Close();
				        socket.Close();
					}
				 }
				 else
					Output.WriteLine("Prediction Data Not Available!");
			
			}
			
			
		}
		
				
	}
}