//+------------------------------------------------------------------+
//|                                                socket_tester.mq4 |
//|                        Copyright 2019, MetaQuotes Software Corp. |
//|                                             https://www.mql5.com |
//+------------------------------------------------------------------+
#property copyright "Copyright 2019, MetaQuotes Software Corp."
#property link      "https://www.mql5.com"
#property version   "1.00"
#property strict


//+----------------------------------------------------------------------------------+
//| Header file for socket library,JSON Serialization and Deserialization            |
//+----------------------------------------------------------------------------------+

#include <socket-library-mt4-mt5.mqh>
#include <JAson.mqh>

//+------------------------------------------------------------------+
//| Enumerated Parameters                                            |
//+------------------------------------------------------------------+
enum Optimizer {
  RMSProp,
  SGD,
  Adam,
  Adagrad,
};
  
enum Architecture {
  LSTM,
  GRU,
  BidirectionalLSTM,
  BidirectionalGRU,
};
 
enum Loss   {
   MSE,
   R2,
};

//+------------------------------------------------------------------+
//| Input Parameters                                                 |
//+------------------------------------------------------------------+

input Architecture architecture = LSTM; // RNN Architecture
input Optimizer optimizer = RMSProp; // Optimizer
input Loss loss = MSE; // Loss Function

input bool gpu = true; // Allow GPU Computations ?
input bool train = true; // Train ?

//Train size must be greater than window_size = 60
input int trainingSize = 500; // Train Size 

input int epochs = 5;  // Epochs
input int scale = 100; // Scale

input string fileName = "model1"; // File Name to export model

input double momentum = 0.9; // Momentum (for SGD)
input double learningRate = 0.001; // Learning Rate 

input double testingPart = 10; // Percentage of Train/Test Split
input double testingWeight = 50; // Percentage of Train/Test Score Weights

input int retrain = 10; // Retrain bar
input int bars = 5; // Future bars to predict

ClientSocket * socket = NULL;
int count = 0;
datetime previousTime;
string previousPred;

//+------------------------------------------------------------------+
//| Retrain Bar Detect Function                                      |
//+------------------------------------------------------------------+

bool onRetrainBar(void){

   if(previousTime != Time[0]){
      previousTime = Time[0]; 
      count++;
   }
   if(count == retrain){
      count = 0;
      return true;
   }
   return false;
}



void drawlr(string res){
   CJAVal json;
   if(!json.Deserialize(res)) {
      Print("BAD RESPONSE !!");
      return;
   }
   double predictions[];
   ArrayResize(predictions, bars);
   
   for(int i=0;i<bars;i++){
      ObjectDelete(ChartID(),"pred" + IntegerToString(i + 1));
   }
   
   for(int i=0;i<bars;i++)
   {
      predictions[i] = NormalizeDouble(StringToDouble(json["Pred"][i].ToStr()), Digits);
      //Print(predictions[i]);
      //Print(TimeCurrent() + ChartPeriod(0)*60*(i+1));
      ObjectCreate(0, "pred" + IntegerToString(i + 1),OBJ_ARROW, 0, TimeCurrent() + ChartPeriod(0)*60*(i+1),  predictions[i]);
      ObjectSetInteger(0, "pred" + IntegerToString(i + 1),OBJPROP_COLOR,clrRed); 
      ObjectSetInteger(ChartID(), "pred" + IntegerToString(i + 1),OBJPROP_WIDTH,3);
      ObjectSetInteger(0, "pred" + IntegerToString(i + 1),OBJPROP_ARROWCODE,159);
   }
}


//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
  {
//---
   
   //EventSetTimer(10);
   previousPred = "";
   
   ObjectCreate(ChartID(),"Trainbutton",OBJ_BUTTON,0,0,0);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_XSIZE,140);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_YSIZE,30);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_XDISTANCE,40);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_YDISTANCE,10);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_COLOR,clrBlue);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_BGCOLOR,clrWhite);
   ObjectSetText("Trainbutton","Train Model",10,NULL,clrBlue);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_STATE,false);

   ObjectCreate(ChartID(),"Predbutton",OBJ_BUTTON,0,0,0);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_XSIZE,140);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_YSIZE,30);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_XDISTANCE,40);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_YDISTANCE,50);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_COLOR,clrBlue);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_BGCOLOR,clrWhite);
   ObjectSetText("Predbutton","Predict",10,NULL,clrBlue);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_STATE,false);   
   
   if(!EventChartCustom(ChartID(),0,0,0,"Trainbutton")){
      Print("Error : ",GetLastError());
   }   
   
   if(!EventChartCustom(ChartID(),0,0,0,"Predbutton")){
      Print("Error : ",GetLastError());
   }     
   
   
//---
   return(INIT_SUCCEEDED);
  }
//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+


void OnTimer(){

   

}


void OnDeinit(const int reason)
  {
//---
   
   for(int i=0;i<bars;i++)
   {
      ObjectDelete(ChartID(),"pred" + IntegerToString(i + 1));
   }
   ObjectDelete(ChartID(),"Trainbutton");
   ObjectDelete(ChartID(),"Predbutton");
   //EventKillTimer();
   delete socket;   
   
  }
//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
  {
//---
      if(onRetrainBar()){
      
         if(!socket){  
            
            socket = new ClientSocket("localhost",9090);
            
            if (socket.IsSocketConnected()){
               Print("Client connection succeeded");
            } 
            else{
               Print("Client connection failed");
            }
            Print("Connected to "," localhost",":",9090);
                  
            double clpr[];
            int copyed = CopyClose(ChartSymbol(ChartID()),ChartPeriod(ChartID()),0,trainingSize,clpr);
                  
            datetime time[];
            int copyTime = CopyTime(_Symbol,PERIOD_CURRENT,0,trainingSize,time);
            
            CJAVal json;
            for (int i = 0; i < ArraySize(clpr); i++)
            {
               json["Data"].Add(DoubleToString(clpr[i], 6));
               json["Time"].Add((string)time[i]);         
            }
            
            json["FileName"] = fileName;
            json["GPU"] = gpu;
            json["Architecture"] = (int)architecture;
            json["Optimizer"] = (int)optimizer;
            json["Loss"] = (int)loss;
            json["LearningRate"] = learningRate;
            json["Epochs"] = epochs;
            json["Scale"] = scale;
            json["Momentum"] = momentum;
            json["TestingPart"] = testingPart;
            json["TestingWeight"] = testingWeight;
            json["Bars"] = bars;
            
            string jsonString = json.Serialize();
            if(socket.Send(jsonString)){
               Print("Data sent successfully for Retrain.");
            }                
            
         }
         else{
         
            Print("Socket Is Busy.");
         
         }   
      
      }
  }
  
//+------------------------------------------------------------------+
//| Expert event function                                            |
//+------------------------------------------------------------------+  

void OnChartEvent(const int id, const long &lparam, const double &dparam, const string &sparam){

   if(id == CHARTEVENT_OBJECT_CLICK && sparam == "Trainbutton"){
      
      if(!socket){  
         
         previousTime = Time[0];
         
         socket = new ClientSocket("localhost",9090);
         
         if (socket.IsSocketConnected()){
            Print("Client connection succeeded");
         } 
         else{
            Print("Client connection failed");
         }
         Print("Connected to "," localhost",":",9090);
               
         double clpr[];
         int copyed = CopyClose(ChartSymbol(ChartID()),ChartPeriod(ChartID()),0,trainingSize,clpr);
               
         datetime time[];
         int copyTime = CopyTime(_Symbol,PERIOD_CURRENT,0,trainingSize,time);
         
         CJAVal json;
         for (int i = 0; i < ArraySize(clpr); i++)
         {
            json["Data"].Add(DoubleToString(clpr[i], 6));
            json["Time"].Add((string)time[i]);         
         }
         
         json["FileName"] = fileName;
         json["GPU"] = gpu;
         json["Architecture"] = (int)architecture;
         json["Optimizer"] = (int)optimizer;
         json["Loss"] = (int)loss;
         json["LearningRate"] = learningRate;
         json["Epochs"] = epochs;
         json["Scale"] = scale;
         json["Momentum"] = momentum;
         json["TestingPart"] = testingPart;
         json["TestingWeight"] = testingWeight;
         json["Bars"] = bars;
         
         string jsonString = json.Serialize();
         if(socket.Send(jsonString)){
            Print("Data sent successfully for Training.");
         }          
         
      }
      else{
      
         Print("Socket Is Busy.");
      
      }

      ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_STATE,false);
      
   }
   
   if(id == CHARTEVENT_OBJECT_CLICK && sparam == "Predbutton"){
   
      if(socket==NULL){
         
         if(previousPred != ""){
            Print("Based on previously trained model, Prediction are : ",previousPred);
            drawlr(previousPred);
         }
         else{
            Print("No predicted data is available or Model is still getting trained.");
         }
         
         ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_STATE,false);
         return;
         
      }
      string strMessage;
      do{
         strMessage = socket.Receive("\r\n");
         if (strMessage != "") {
            previousPred = strMessage;
            Print(strMessage);
            drawlr(strMessage);
            delete socket;
            socket = NULL;
         }
         else{
         
            if(previousPred != ""){
               Print("Based on previously trained model, Prediction are : ",previousPred);
               drawlr(previousPred);
            }
            else{
               Print("No predicted data available or Model is still getting trained.");
            }
         
         }
      }while(socket && strMessage != "");   
      
      ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_STATE,false);
      
   }   

}
  
  
  
//+------------------------------------------------------------------+



