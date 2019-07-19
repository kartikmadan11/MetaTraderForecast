//+------------------------------------------------------------------+
//|                                               ForecastExpert.mq5 |
//|                                                             HPCS |
//|                                             https://www.mql5.com |
//+------------------------------------------------------------------+
#property copyright "HPCS"
#property link      "https://www.mql5.com"
#property version   "1.00"

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
//| Expert initialization function                                   |
//+------------------------------------------------------------------+

// Header file for JSON Serialization and Deserialization
#include <JAson.mqh>

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

int socket = -2; // Socket Variable 
int count = 0;
datetime previousTime;
string previousPred;

//+------------------------------------------------------------------+
//| Retrain Bar Detect Function                                      |
//+------------------------------------------------------------------+

bool onRetrainBar(void){
         
   if(previousTime != iTime(ChartSymbol(ChartID()),Period(),0)){
      previousTime = iTime(ChartSymbol(ChartID()),Period(),0); 
      count++;
   }
   if(count == retrain){
      count = 0;
      return true;
   }
   return false;
}

// Socket Send Function
bool socksend(int sock,string request) {
   char req[];
   int  len=StringToCharArray(request,req)-1;
   if(len<0) 
      return(false);
   return(SocketSend(sock,req,len)==len); 
}

// Socket Receive Function
string socketreceive(int sock,int timeout)   {
   char rsp[];
   string result="";
   uint len;
   uint timeout_check=GetTickCount()+timeout;
   do
   {
      len=SocketIsReadable(sock);
      if(len)
      {
         int rsp_len;
         rsp_len=SocketRead(sock,rsp,len,timeout);
         if(rsp_len>0) 
         {
            result+=CharArrayToString(rsp,0,rsp_len); 
         }
      }
   }
   while((GetTickCount()<timeout_check) && !IsStopped());
   return result;
}

void drawpred(string res)
{
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
      predictions[i] = NormalizeDouble(StringToDouble(json["Pred"][i].ToStr()), Digits());
      //Print(predictions[i]);
      //Print(TimeCurrent() + ChartPeriod(0)*60*(i+1));
      ObjectCreate(0, "pred" + IntegerToString(i + 1),OBJ_ARROW, 0, TimeCurrent() + ChartPeriod(0)*60*(i+1),  predictions[i]);
      ObjectSetInteger(0, "pred" + IntegerToString(i + 1),OBJPROP_COLOR,clrRed); 
      ObjectSetInteger(ChartID(), "pred" + IntegerToString(i + 1),OBJPROP_WIDTH,3);
      ObjectSetInteger(0, "pred" + IntegerToString(i + 1),OBJPROP_ARROWCODE,159);
   }
}

int OnInit()
{
//---

   previousPred = "";
   
   ObjectCreate(ChartID(),"Trainbutton",OBJ_BUTTON,0,0,0);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_XSIZE,140);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_YSIZE,30);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_XDISTANCE,40);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_YDISTANCE,10);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_COLOR,clrBlue);
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_BGCOLOR,clrWhite);
   ObjectSetString(ChartID(),"Trainbutton",OBJPROP_TEXT,"TRAIN MODEL");
   ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_STATE,false);

   ObjectCreate(ChartID(),"Predbutton",OBJ_BUTTON,0,0,0);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_XSIZE,140);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_YSIZE,30);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_XDISTANCE,40);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_YDISTANCE,50);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_COLOR,clrBlue);
   ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_BGCOLOR,clrWhite);
   ObjectSetString(ChartID(),"Predbutton",OBJPROP_TEXT,"PREDICT");
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
void OnDeinit(const int reason)
{
//---   
   SocketClose(socket);
   ObjectsDeleteAll(ChartID(),-1,-1);
   
}
//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
{
//---

   if(onRetrainBar()){
   
      //Print("Inside onRetrainBar.");
   
      if(socket == -2){
      
         socket = SocketCreate();
         if(socket!=INVALID_HANDLE) {
            if(SocketConnect(socket,"localhost",9090,1000)) {
               Print("Connected to "," localhost",":",9090);
                  
               double clpr[];
               int copyClose = CopyClose(ChartSymbol(ChartID()),PERIOD_CURRENT,0,trainingSize,clpr);
               
               datetime time[];
               int copyTime = CopyTime(ChartSymbol(ChartID()),PERIOD_CURRENT,0,trainingSize,time);
               
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
       
               bool send = socksend(socket, jsonString);
               if(send)
                  Print("Data Sent Successfully For Retrain.");
                
            }
            else{
               socket = -2;
               Print("Error in connecting to ","localhost",":",9090,"  Error  :  ",GetLastError());
            }     
         }
         else{
            socket = -2;
            Print("Socket creation error ",GetLastError());
         } 
         
      }
      else{
      
         Print("Socket Is Busy.");
      
      }
   
   }


} 
//+------------------------------------------------------------------+

void OnTimer(){

}


void OnChartEvent(const int id, const long &lparam, const double &dparam, const string &sparam){

   if(id == CHARTEVENT_OBJECT_CLICK && sparam == "Trainbutton"){
   
      if(socket == -2){
     
         previousTime = iTime(ChartSymbol(ChartID()),Period(),0);
         socket = SocketCreate();
         if(socket!=INVALID_HANDLE) {
            if(SocketConnect(socket,"localhost",9090,1000)) {
               Print("Connected to "," localhost",":",9090);
                   
               double clpr[];
               int copyClose = CopyClose(_Symbol,PERIOD_CURRENT,0,trainingSize,clpr);
               
               datetime time[];
               int copyTime= CopyTime(_Symbol,PERIOD_CURRENT,0,trainingSize,time);
               
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
               //Print(jsonString);
               bool send = socksend(socket, jsonString);
               if(send)
                  Print("Data Sent Successfully.");
                
            }
            else{
               socket = -2; 
               Print("Error in connecting to ","localhost",":",9090,"  Error  :  ",GetLastError()); 
            }    
         }
         else{
            socket = -2; 
            Print("Socket creation error ",GetLastError()); 
         }
      }
      else{
      
         Print("Socket Is Busy In Training.");
      
      }
      ObjectSetInteger(ChartID(),"Trainbutton",OBJPROP_STATE,false);
   
   }
   
   if(id == CHARTEVENT_OBJECT_CLICK && sparam == "Predbutton"){
   
      if(socket==-2){
         
         if(previousPred != ""){
            Print("Based on previously trained model, Prediction are : ",previousPred);
            drawpred(previousPred);
         }
         else{
            Print("No predicted data is available or Model is still getting trained.");
         }
         
         ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_STATE,false);
         return;
         
      }
      string strMessage;
      do{
         strMessage = socketreceive(socket,10);
         if (strMessage != "") {
            previousPred = strMessage;
            Print(strMessage);
            drawpred(strMessage);
            SocketClose(socket);
            socket = -2;
         }
         else{
         
            if(previousPred != ""){
               Print("Based on previously trained model, Prediction are : ",previousPred);
               drawpred(previousPred);
            }
            else{
               Print("No predicted data available or Model is still getting trained.");
            }
         
         }
      }while(socket != -2 && strMessage != "");   
      
      ObjectSetInteger(ChartID(),"Predbutton",OBJPROP_STATE,false);
   
   }

}

//+------------------------------------------------------------------+
