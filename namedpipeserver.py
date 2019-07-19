import win32pipe, win32file
pipe_name = 'testpipe'

pipe = win32pipe.CreateNamedPipe(r'\\.\\pipe\\testpipe', win32pipe.PIPE_ACCESS_DUPLEX, win32pipe.PIPE_TYPE_MESSAGE | win32pipe.PIPE_WAIT, 1, 65536, 65536, 300, None)

win32pipe.ConnectNamedPipe(pipe, None)

win32file.ReadFile(pipe, 2000)