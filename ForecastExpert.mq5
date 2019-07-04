//+------------------------------------------------------------------+
//|                                             RegressionExpert.mq5 |
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
};
  
enum Architecture {
  LSTM,
  GRU,
  BidirectionalLSTM,
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

bool gpu = true;
bool train = true;

int trainingSize = 5000; // must be greater than window_size = 60
int epochs = 50;
int scale = 100;

string fileName = "model1";

double momentum = 0.1;
double learningRate = 0.001;

double testingPart = 10;
double testingWeight = 50;


bool socksend(int sock,string request) {
   char req[];
   int  len=StringToCharArray(request,req)-1;
   if(len<0) 
      return(false);
   return(SocketSend(sock,req,len)==len); 
}

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

void drawlr(string points) 
{
   string res[];
   StringSplit(points,' ',res);
   if(ArraySize(res)==2) 
   {
      Print(StringToDouble(res[0]));
      Print(StringToDouble(res[1]));
      datetime temp[];
      CopyTime(Symbol(),Period(),TimeCurrent(),trainingSize,temp);
      ObjectCreate(0,"regrline",OBJ_TREND,0,TimeCurrent(),NormalizeDouble(StringToDouble(res[0]),_Digits),temp[0],NormalizeDouble(StringToDouble(res[1]),_Digits)); 
   }
}

int OnInit()
{
//---
int socket = SocketCreate();
if(socket!=INVALID_HANDLE) {
   if(SocketConnect(socket,"localhost",9090,1000)) {
      Print("Connected to "," localhost",":",9090);
            
      double clpr[];
      int copyClose = CopyClose(_Symbol,PERIOD_CURRENT,0,trainingSize,clpr);
      
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
      json["Architecture"] = (int)LSTM;
      json["Optimizer"] = (int)RMSProp;
      json["LearningRate"] = learningRate;
      json["Epochs"] = epochs;
      json["Scale"] = scale;
      json["Momentum"] = momentum;
      json["TestingPart"] = testingPart;
      json["TestingWeight"] = testingWeight;
      
      string jsonString = json.Serialize();
      
      string received = socksend(socket, jsonString) ? socketreceive(socket, 10) : ""; 
      Print(received);   
   }
   else 
      Print("Connection ","localhost",":",9090," error ",GetLastError());
   SocketClose(socket);       
   }
else 
   Print("Socket creation error ",GetLastError()); 
//---
   return(INIT_SUCCEEDED);
}
//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
  {
//---   
  }
//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
{
//---
} 
//+------------------------------------------------------------------+