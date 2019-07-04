# Python Build for training, testing and exporting model

# Importing Libraries 
import numpy as np
import pandas as pd
import tensorflow as tf
import pickle
from enum import Enum

from datetime import datetime

from keras.models import Sequential
from keras.layers import Dense, Dropout, LSTM, CuDNNLSTM, GRU, CuDNNGRU, Bidirectional
from keras.optimizers import SGD, RMSprop
from keras.models import load_model
from keras import backend as K

from sklearn.metrics import mean_squared_error
from sklearn.preprocessing import MinMaxScaler  

import matplotlib.pyplot as plt

import os

# Defining Enums
class Architecture(Enum):
    LSTM = 0
    GRU = 1
    BidirectionalLSTM = 2

class Optimizer(Enum):
    RMSProp = 0
    SGD = 1

class Loss(Enum):
    MSE = 0
    R2 = 1

# Just disables the warning, doesn't enable AVX/FMA
os.environ['TF_CPP_MIN_LOG_LEVEL'] = '2'

# Suppressing deprecated warnings
tf.compat.v1.logging.set_verbosity(tf.compat.v1.logging.ERROR)

# Allowing Cudnn for LSTM
config = tf.ConfigProto()
config.gpu_options.allow_growth = True
session = tf.Session(config=config)

window_size = 60

def getOptimizer(optimizer, lr, momentum):
    if optimizer == 0:
        return RMSprop(lr = lr)
    else:
        return SGD()

def getModel(X_train, architecture, isCuda):
    if architecture == Architecture.LSTM.value:
        if isCuda:
            # The LSTM architecture
            regressor = Sequential()
            # First LSTM layer with Dropout regularisation
            regressor.add(CuDNNLSTM(units=50, return_sequences=True, input_shape=(X_train.shape[1],1)))
            regressor.add(Dropout(0.2))
            # Second LSTM layer
            regressor.add(CuDNNLSTM(units=50, return_sequences=True))
            regressor.add(Dropout(0.2))
            # Third LSTM layer
            regressor.add(CuDNNLSTM(units=50, return_sequences=True))
            regressor.add(Dropout(0.2))
            # Fourth LSTM layer 
            regressor.add(CuDNNLSTM(units=50))
            regressor.add(Dropout(0.2))
            # The output layer
            regressor.add(Dense(units=1))
            return regressor
        else:
            # The LSTM architecture
            regressor = Sequential()
            # First LSTM layer with Dropout regularisation
            regressor.add(LSTM(units=50, return_sequences=True, input_shape=(X_train.shape[1],1)))
            regressor.add(Dropout(0.2))
            # Second LSTM layer
            regressor.add(LSTM(units=50, return_sequences=True))
            regressor.add(Dropout(0.2))
            # Third LSTM layer
            regressor.add(LSTM(units=50, return_sequences=True))
            regressor.add(Dropout(0.2))
            # Fourth LSTM layer 
            regressor.add(LSTM(units=50))
            regressor.add(Dropout(0.2))
            # The output layer
            regressor.add(Dense(units=1))
            return regressor
    
    elif architecture == Architecture.GRU.value:
        if isCuda:
            # The GRU architecture
            regressorGRU = Sequential()
            # First GRU layer with Dropout regularisation
            regressorGRU.add(CuDNNGRU(units=50, return_sequences=True, input_shape=(X_train.shape[1],1)))
            regressorGRU.add(Dropout(0.2))
            # Second GRU layer
            regressorGRU.add(CuDNNGRU(units=50, return_sequences=True, input_shape=(X_train.shape[1],1)))
            regressorGRU.add(Dropout(0.2))
            # Third GRU layer
            regressorGRU.add(CuDNNGRU(units=50, return_sequences=True, input_shape=(X_train.shape[1],1)))
            regressorGRU.add(Dropout(0.2))
            # Fourth GRU layer
            regressorGRU.add(CuDNNGRU(units=50))
            regressorGRU.add(Dropout(0.2))
            # The output layer
            regressorGRU.add(Dense(units=1))
            return regressorGRU
        else:
            # The GRU architecture
            regressorGRU = Sequential()
            # First GRU layer with Dropout regularisation
            regressorGRU.add(GRU(units=50, return_sequences=True, input_shape=(X_train.shape[1],1), activation='tanh'))
            regressorGRU.add(Dropout(0.2))
            # Second GRU layer
            regressorGRU.add(GRU(units=50, return_sequences=True, input_shape=(X_train.shape[1],1), activation='tanh'))
            regressorGRU.add(Dropout(0.2))
            # Third GRU layer
            regressorGRU.add(GRU(units=50, return_sequences=True, input_shape=(X_train.shape[1],1), activation='tanh'))
            regressorGRU.add(Dropout(0.2))
            # Fourth GRU layer
            regressorGRU.add(GRU(units=50, activation='tanh'))
            regressorGRU.add(Dropout(0.2))
            # The output layer
            regressorGRU.add(Dense(units=1))
            return regressorGRU


def getScaledData(training_set, scale, file_name):

    sc = MinMaxScaler(feature_range=(0,1))
    training_set_scaled = sc.fit_transform(training_set)
    
    pickle_out = open(file_name + '_scaler.pickle', 'wb')
    pickle.dump(sc, pickle_out)
    pickle_out.close()

    # creating a data structure with window_size timesteps and 1 output
    # for each element of training set, we have window_size previous training set elements 
    X_train = []
    Y_train = []
    for i in range(window_size,training_set_scaled.shape[0]):
        X_train.append(training_set_scaled[i-window_size:i,0])
        Y_train.append(training_set_scaled[i,0])
    X_train, Y_train = np.array(X_train), np.array(Y_train)

    # Reshaping X_train for efficient modelling
    X_train = np.reshape(X_train, (X_train.shape[0],X_train.shape[1],1))

    return X_train, Y_train

def save_plot(test,predicted, file_name):
    plt.plot(test, color='red',label='Real Stock Price')
    plt.plot(predicted, color='blue',label='Predicted Stock Price')
    plt.title('Stock Price Prediction')
    plt.xlabel('Time')
    plt.ylabel('Stock Price')
    plt.legend()
    plt.savefig(file_name + '.jpg')

def r2_score(y_true, y_pred):
    SS_res =  K.sum(K.square(y_true - y_pred)) 
    SS_tot = K.sum(K.square(y_true - K.mean(y_true))) 
    return ( 1 - SS_res/(SS_tot + K.epsilon()) )
    
def train(training_set, date, lr, scale, epochs, momentum, optimizer, file_name, architecture):
    if(type(training_set) == list and type(date) == list):

        # Constructing a pandas dataframe for reusability and reference
        df = pd.DataFrame(data = training_set, columns = ['Feature'], index = pd.to_datetime(date))
        df.index.names = ['Date']
        df.index = pd.to_datetime(df.index)
        df.to_csv(file_name + '.csv')

        training_set = df.values

        # Scaling and preprocessing the training set
        X_train, Y_train = getScaledData(training_set, scale, file_name)
        
        # Constructing a stacked LSTM Sequential Model
        regressor = getModel(X_train, architecture, tf.test.is_gpu_available())

        # Compiling the RNN
        regressor.compile(optimizer=getOptimizer(optimizer, lr, momentum), loss='mean_squared_error', metrics=['mse',r2_score])
            
        # Fitting to the training set
        hist = regressor.fit(X_train, Y_train,epochs = epochs, batch_size=32)

        #Saving trained model
        regressor.save(file_name + '.h5')
        
        pickle_out = open(file_name + '_trainhist.pickle', 'wb')
        pickle.dump(hist.history, pickle_out)
        pickle_out.close()
        
        #Deleting model instance
        del regressor

        return 100    
    else:
        return 110

def test(testing_set, date, file_name):
    if(type(testing_set) == list and type(date) == list):

        # Constructing a pandas dataframe for reusability and reference
        df = pd.DataFrame(data = testing_set, columns = ['Feature'], index = date)
        df.index.names = ['Date']
        df.index = pd.to_datetime(df.index)
        test_set = df['Feature'].values
        
        prev_dataset = pd.read_csv(file_name + '.csv', index_col = 'Date', parse_dates=['Date'])
        
        regressor = load_model(file_name + '.h5', custom_objects={'r2_score':r2_score})

        file = open(file_name + '_scaler.pickle', 'rb')
        scaler = pickle.load(file)
        file.close()

        # Now to get the test set ready in a similar way as the training set.
        dataset_total = pd.concat((prev_dataset, df),axis=0, sort=False)
        dataset_total.to_csv(file_name + '.csv')

        inputs = dataset_total[len(dataset_total)-len(testing_set) - window_size:]['Feature'].values
        inputs = inputs.reshape(-1,1)
        inputs  = scaler.transform(inputs)

        # Preparing X_test and predicting the prices
        X_test = []
        for i in range(window_size, inputs.shape[0]):
            X_test.append(inputs[i - window_size:i,0])
        X_test = np.array(X_test)
        X_test = np.reshape(X_test, (X_test.shape[0],X_test.shape[1],1))
        
        predicted_stock_price = regressor.predict(X_test)
        predicted_stock_price = scaler.inverse_transform(predicted_stock_price)

        eval = regressor.evaluate(X_test, scaler.transform(test_set.reshape(-1,1)))

        pickle_out = open(file_name + '_testhist.pickle', 'wb')
        pickle.dump(eval, pickle_out)
        pickle_out.close()

        # Deleting model instance
        del regressor
        
        return 100
    else:
        return 110

def evaluate(file_name, testing_weight):

    file = open(file_name + '_trainhist.pickle', 'rb')
    trainHistory = pickle.load(file)
    file.close()

    file = open(file_name + '_testhist.pickle', 'rb')
    testScores = pickle.load(file)
    file.close()

    trainScores = [trainHistory[key][-1] for key in trainHistory.keys()]
    scoreList = [(trainScores[i] * (1 - testing_weight/100) + testScores[i] * (testing_weight/100))/2 for i in range(len(trainScores))]
    
    return scoreList

def predict(file_name, ticks):
    if(ticks < window_size):
        
        prev_dataset = pd.read_csv(file_name + '.csv', index_col = 'Date', parse_dates=['Date'])
        
        regressor = load_model(file_name + '.h5', custom_objects={'r2_score':r2_score})

        file = open(file_name + '_scaler.pickle', 'rb')
        scaler = pickle.load(file)
        file.close()

        inputs = prev_dataset[len(prev_dataset) - ticks - window_size:]['Feature'].values
        inputs = inputs.reshape(-1,1)
        inputs  = scaler.transform(inputs)
        
        # Preparing X_pred and predicting the prices
        X_pred = []
        for i in range(window_size, inputs.shape[0]):
            X_pred.append(inputs[i - window_size:i,0])
        X_pred = np.array(X_pred)
        X_pred = np.reshape(X_pred, (X_pred.shape[0],X_pred.shape[1],1))
        
        predicted_stock_price = regressor.predict(X_pred)
        predicted_stock_price = scaler.inverse_transform(predicted_stock_price)
        
        return predicted_stock_price.reshape(predicted_stock_price.shape[0]).tolist()
    else:
        return -1
