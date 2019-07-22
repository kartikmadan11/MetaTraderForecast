from include.pywpipe import wpipe
import win32file
import win32pipe
import pipes

pipe_name = r'\\.\pipe\test_pipe'

pserver = wpipe.Server(pipe_name, wpipe.Mode.Slave)
print('Created named pipe server')
while True:
    for client in pserver:
        while client.canread():
            rawmsg = client.read()
            client.write(b'hallo')    
    pserver.waitfordata()
pserver.shutdown()
print(rawmsg)