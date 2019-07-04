import socket
import ast
import build

def train_test_model(msg = ''):
    msg = msg.replace('true','True')
    msg = ast.literal_eval(msg)
    
    print(type(msg))
    
    if(type(msg) == dict):
        input_data = msg
    else:
        return "BAD JSON!!"
    
    data = input_data['Data']
    date = input_data['Time']
    file_name = input_data['FileName']
    
    testSize = int(input_data['TestingPart'] / 100 * len(data))
    trainSize = len(data) - testSize
    
    train = build.train(training_set=data[:trainSize], date=date[:trainSize], lr=input_data['LearningRate'], scale=input_data['Scale'], epochs=input_data['Epochs'], momentum=input_data['Momentum'], optimizer=input_data['Optimizer'], file_name=file_name, architecture=input_data['Architecture'])
    test = build.test(testing_set=data[trainSize:], date=date[trainSize:], file_name=input_data['FileName'])

    print(train)
    print(test)
    
    evaluate = build.evaluate(file_name=file_name, testing_weight=input_data['TestingWeight'])

    print(evaluate)
    
    return str(evaluate)

class socketserver:
    def __init__(self, address = '', port = 9090):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.address = address
        self.port = port
        self.sock.bind((self.address, self.port))
        self.cummdata = ''
        
    def recvmsg(self):
        self.sock.listen(1)
        self.conn, self.addr = self.sock.accept()
        print('connected to', self.addr)
        self.cummdata = ''

        while True:
            data = self.conn.recv(10000000)
            self.cummdata+=data.decode("utf-8")
            if not data:
                break    
            self.conn.send(bytes(train_test_model(self.cummdata), "utf-8"))
            return self.cummdata
            
    def __del__(self):
        self.sock.close()
        
serv = socketserver('127.0.0.1', 9090)

while True:  
    msg = serv.recvmsg()