#region Using declarations
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Chart;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using Newtonsoft.Json;

using System.ServiceModel.Web;
using System.Collections.Generic;
using System.Text;
#endregion

// This namespace holds all indicators and is required. Do not change it.
namespace NinjaTrader.Indicator
{
    /// <summary>
    /// Indicator for RNN Project 
    /// </summary>
    [Description("Indicator for RNN Project ")]
    public class RNNProject_NT7_Indi : Indicator
    {
        #region Variables
        // Wizard generated variables
        // User defined variables (add any user defined variables below)
		private E_Architecture architecture = E_Architecture.LSTM; // RNN Architecture
		private E_Optimizer optimizer  = E_Optimizer.RMSProp; // Optimizer
		private E_Loss loss = E_Loss.MSE;
			
		private bool gpu    = true; // Allow GPU Computations ?
		private bool train  = true; // Train ?

		private bool isTrained = false;

		//Train size must be greater than window_size = 60
		private int trainingSize = 500 ; // Train Size 
		private int epochs       = 10;  // Epochs
		private int scale        = 100; // Scale

		private string fileName = "model1"; // File Name to export model

		private double momentum      = 0.9; // Momentum (for SGD)
		private double learningRate  = 0.001; // Learning Rate 
		private double testingPart   = 10; // Percentage of Train/Test Split
		private double testingWeight = 50; // Percentage of Train/Test Score Weights

		private int bars            = 5;
		private int prevTrain       = 0;
		private int retrainInterval = 10;
			
		// For Connection with socket
		public TcpClient socket;
		public NetworkStream stream;

        #endregion
		
		#region Class Definition

		// Parameters to be sent to the model for training
		public class trainParameters
		{
			public List<string> Data{ get; set;}
			public List<string> Time{get; set;}
			
			public string FileName {get; set;}

			public bool GPU {get; set;}
			public bool Train {get; set;}

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

        /// <summary>
        /// This method is used to configure the indicator and is called once before any bar data is loaded.
        /// </summary>
        protected override void Initialize()
        {
            Overlay				= false;
			CalculateOnBarClose = true;
        }

        /// <summary>
        /// Called on each bar update event (incoming tick)
        /// </summary>
        protected override void OnBarUpdate()
        {
		//For Training on Real-Time Data	
		if(Historical)
			return;

		// Collect Enough Data
		if (CurrentBar < trainingSize)
			return;
			
		int interval = CurrentBar - prevTrain;

		if (!isTrained ||(isTrained && interval == retrainInterval))
		{
			// Establishing connection				
			socket = new TcpClient();
			socket.Connect("localhost", 9090);
			stream = socket.GetStream();

			if (socket.Connected)
	            {
				Print("connected!");

				List<string> closePrice = new List<string>();
				List<string> time = new List<string>();
				for (int index = 0; index < trainingSize; index++) 
				{
					 closePrice.Add(Close[index].ToString() );	
					 time.Add(Time[index].ToString());
				}
					
				// Storing the parameters for training
				var jsonObject = new trainParameters();				

				jsonObject.Data = closePrice;
				jsonObject.Time = time;
				jsonObject.FileName = fileName;
				jsonObject.GPU = gpu;
				jsonObject.Train = train;
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

				// Serializing to JSON
				string jsonString = JsonConvert.SerializeObject(jsonObject);
				Byte[] data = Encoding.UTF8.GetBytes(jsonString);

				stream.Write(data, 0, data.Length);		         
				Print("Sent : " + jsonString);

				isTrained = true;
				prevTrain = CurrentBar;				
			}	
				
			else
			{
			   Print("connection failed!");
			}
		}
		// Receiving data after training from the server  
		else if(socket.Connected)
		{
			if(stream.DataAvailable)
			{
				byte[] data = new Byte[2*256];
				string response = "";
				Int32 bytes = stream.Read(data, 0, data.Length);
				if(bytes > 1)
				response = Encoding.UTF8.GetString(data,0,bytes);

				if(response != "")
				{ 
					Print("Received : " + response);
					var jsonObject = new PredictionParameters();

					// Deserializing JSON data 
					jsonObject = JsonConvert.DeserializeObject<PredictionParameters>(response);

					// Plotting the predictions on the chart
					for (int i=-1;i>=-1*bars;i--)
					{
						double ypred = double.Parse(jsonObject.Pred[(-1*i)-1].ToString());
						DrawDot("Prediction " + i.ToString(), true, i, ypred, Color.Cyan);

					} 

					// closing the socket
					stream.Close();
					socket.Close();
				}
				else
					Print("Not Received!");
			}
			else 
				Print("Prediction Not Available!");
		}		
		else
			Print("Socket Disconnected! ");
        }

        #region Properties
		
		[Description("Architecture of the Training Model")]
		[Category("Model Parameters")]
	        public E_Architecture Architecture
		{
			get { return architecture; }
		    set { architecture = value; }
		}
		
		[Description("Optimizer to be Used")]
		[Category("Model Parameters")]
		public E_Optimizer Optimizer
		{
			get { return optimizer; }
		    set { optimizer = value; }
		}

		[Description("Loss Function")]
		[Category("Model Parameters")]
		public E_Loss Loss
		{
			get { return loss; }
		    set { loss = value; }
		}

		[Description("If GPU is enabled")]
		[Category("Model Parameters")]
		public bool Gpu
		{
			get {return gpu;}
			set{gpu = value;}
		}

		[Description("If training is enabled")]
		[Category("Model Parameters")]
		public bool Train
		{
			get {return train;}
			set{train = value;}
		}

		[Description("Size of data to be sent for training")]
		[Category("Model Parameters")]
		public int Training_Size
		{
			get {return trainingSize;}
			set{ trainingSize = value;}
		}

		[Description("Epochs")]
		[Category("Model Parameters")]
		public int Epochs
		{
			get {return epochs;}
			set{epochs = value;}
		}

		[Description("Scaling Parameter")]
		[Category("Model Parameters")]
		public int Scale
		{
			get {return scale;}
			set{scale = value;}
		}

		[Description("Number of future bars to predict")]
		[Category("Model Parameters")]
		public int Bars_To_Predict
		{
			get {return bars;}
			set{bars = value;}
		}

		[Description("Momentum")]
		[Category("Model Parameters")]
		public double Momentum
		{
			get {return momentum;}
			set{momentum = value;}
		}

		[Description("Learning Rate for the model")]
		[Category("Model Parameters")]
		public double Learning_Rate
		{
			get {return learningRate;}
			set{learningRate = value;}
		}

		[Description("Train/Test data split (in percentage)")]
		[Category("Model Parameters")]
		public double Testing_Part
		{
			get {return testingPart ;}
			set{ testingPart = value;}
		}

		[Description("Train/Test score(in percentage)")]
		[Category("Model Parameters")]
		public double Testing_Weight
		{
			get {return testingWeight;}
			set{testingWeight = value;}
		}

		[Description("Name of file to store Model")]
		[Category("Model Parameters")]
		public string FileName
		{
			get {return fileName;}
			set {fileName = value;}
		}
		
		[Description("The Number Of Bars after which to retrain")]
		[Category("Model Parameters")]
		public int Retrain_Interval
		{
			get {return retrainInterval;}
			set{retrainInterval = value;}
		}

        #endregion
    }
}

	#region Enum Declaration
		public enum E_Optimizer
		{
		  RMSProp,
		  SGD,
		  Adam,
		  Adagrad
		};
			  
		public enum E_Architecture 
		{
		  LSTM,
		  GRU,
		  BidirectionalLSTM,
		  BidirectionalGRU
		};

		public enum E_Loss   
		{
		   MSE,
		   R2
		};
	#endregion

#region NinjaScript generated code. Neither change nor remove.
// This namespace holds all indicators and is required. Do not change it.
namespace NinjaTrader.Indicator
{
    public partial class Indicator : IndicatorBase
    {
        private RNNProject_NT7_Indi[] cacheRNNProject_NT7_Indi = null;

        private static RNNProject_NT7_Indi checkRNNProject_NT7_Indi = new RNNProject_NT7_Indi();

        /// <summary>
        /// Indicator for RNN Project 
        /// </summary>
        /// <returns></returns>
        public RNNProject_NT7_Indi RNNProject_NT7_Indi()
        {
            return RNNProject_NT7_Indi(Input);
        }

        /// <summary>
        /// Indicator for RNN Project 
        /// </summary>
        /// <returns></returns>
        public RNNProject_NT7_Indi RNNProject_NT7_Indi(Data.IDataSeries input)
        {
            if (cacheRNNProject_NT7_Indi != null)
                for (int idx = 0; idx < cacheRNNProject_NT7_Indi.Length; idx++)
                    if (cacheRNNProject_NT7_Indi[idx].EqualsInput(input))
                        return cacheRNNProject_NT7_Indi[idx];

            lock (checkRNNProject_NT7_Indi)
            {
                if (cacheRNNProject_NT7_Indi != null)
                    for (int idx = 0; idx < cacheRNNProject_NT7_Indi.Length; idx++)
                        if (cacheRNNProject_NT7_Indi[idx].EqualsInput(input))
                            return cacheRNNProject_NT7_Indi[idx];

                RNNProject_NT7_Indi indicator = new RNNProject_NT7_Indi();
                indicator.BarsRequired = BarsRequired;
                indicator.CalculateOnBarClose = CalculateOnBarClose;
#if NT7
                indicator.ForceMaximumBarsLookBack256 = ForceMaximumBarsLookBack256;
                indicator.MaximumBarsLookBack = MaximumBarsLookBack;
#endif
                indicator.Input = input;
                Indicators.Add(indicator);
                indicator.SetUp();

                RNNProject_NT7_Indi[] tmp = new RNNProject_NT7_Indi[cacheRNNProject_NT7_Indi == null ? 1 : cacheRNNProject_NT7_Indi.Length + 1];
                if (cacheRNNProject_NT7_Indi != null)
                    cacheRNNProject_NT7_Indi.CopyTo(tmp, 0);
                tmp[tmp.Length - 1] = indicator;
                cacheRNNProject_NT7_Indi = tmp;
                return indicator;
            }
        }
    }
}

// This namespace holds all market analyzer column definitions and is required. Do not change it.
namespace NinjaTrader.MarketAnalyzer
{
    public partial class Column : ColumnBase
    {
        /// <summary>
        /// Indicator for RNN Project 
        /// </summary>
        /// <returns></returns>
        [Gui.Design.WizardCondition("Indicator")]
        public Indicator.RNNProject_NT7_Indi RNNProject_NT7_Indi()
        {
            return _indicator.RNNProject_NT7_Indi(Input);
        }

        /// <summary>
        /// Indicator for RNN Project 
        /// </summary>
        /// <returns></returns>
        public Indicator.RNNProject_NT7_Indi RNNProject_NT7_Indi(Data.IDataSeries input)
        {
            return _indicator.RNNProject_NT7_Indi(input);
        }
    }
}

// This namespace holds all strategies and is required. Do not change it.
namespace NinjaTrader.Strategy
{
    public partial class Strategy : StrategyBase
    {
        /// <summary>
        /// Indicator for RNN Project 
        /// </summary>
        /// <returns></returns>
        [Gui.Design.WizardCondition("Indicator")]
        public Indicator.RNNProject_NT7_Indi RNNProject_NT7_Indi()
        {
            return _indicator.RNNProject_NT7_Indi(Input);
        }

        /// <summary>
        /// Indicator for RNN Project 
        /// </summary>
        /// <returns></returns>
        public Indicator.RNNProject_NT7_Indi RNNProject_NT7_Indi(Data.IDataSeries input)
        {
            if (InInitialize && input == null)
                throw new ArgumentException("You only can access an indicator with the default input/bar series from within the 'Initialize()' method");

            return _indicator.RNNProject_NT7_Indi(input);
        }
    }
}
#endregion
