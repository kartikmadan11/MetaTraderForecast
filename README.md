# MetaTraderForecast
Project that involves training, testing, evaluating and forecasting time series forex data from MetaTrader using Sockets to connect with Python.  An RNN Model of various architecture like LSTM, Bidirectional and GRU can be created. Supports CUDA Computation.

## What does the project do?
MQL based Expert Advisors are attached to chart to make RNN based predictions. All machine learning parameters can be tuned from the input interface of the EA. The model can be trained and exported as a serialized as .hdf5 file. Tests are run simultaneously with initial train and the data is split based on Testing Part (in %) parameter. The model can be evaluated on any number of metrics as provided in the input screen. The model can be loaded to retrain or forecast any number of future bars. 

## Recurrent Neural Network Specs
The various parameter that can be opted for tuning the ML model are -
### RNN Architecture
- **Long Short Term Memory** 
- **Gated Recurrent Units**
- **Bidirectional LSTM**
- **Bidirectional GRU**
### Losses
- **Mean Sqaure Error**
- **R^2 Score**
### Optimizers
- **RMSProp** (Recommended)
- **Stochastic Gradient Descent** (Momentum can be specified)
- **Adam**
- **Adagrad**
### Learning Rate
Specify amount of change to the model during each step of this search process, or the step size

### GPU Computations
For faster computations, GPU can be used. CUDA Support is required. CuDNN implemented RNN layers are used if GPU is opted

### Input screen of EA

![Inputs of EA](/assets/EA_inputs.png)

## Setting up the project
To run the forecaster, run the socketserver.py and wait for socket to be created. Now, attach the EA to the MT4/MT5 platform and specify the parameters for building the model. The predicted values are displayed on the same chart window. 

`python socketserver.py`
### Test GPU Support
To install tensorflow-gpu use python package manager 

`pip install tensorflow-gpu`

If tensorflow-gpu is present, run the following commands to check

`import tensorflow as tf`

`tf.test.is_gpu_available()`
### Dependencies
- Python 3.6 or higher (dev versions not recommended, use stable releases)
- Tensorflow 1.14 or higher
- Keras

**For GPU Support**

- tensorflow-gpu
- CUDA 9.0 or higher (10.0 recommended)
- cuDNN Library v7.4 or higher

### Workflow
![Forecast Workflow](/assets/MetaTraderForecasting.jpg)
