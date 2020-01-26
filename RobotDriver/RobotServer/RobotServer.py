import socketserver
from protocol_pb2 import Steering,Image
from google.protobuf.any_pb2 import Any
from threading import Lock,Thread
import time
import cv2
import array
from numproto import ndarray_to_proto, proto_to_ndarray
import traceback
import serial

global connectedSocket, connectedSocketLock, ser

connectedSocket=None
connectedSocketLock = Lock()

class MyTCPHandler(socketserver.BaseRequestHandler):

    def handle(self):
        global connectedSocket, connectedSocketLock

        connectedSocketLock.acquire()
        try:
            connectedSocket=self.request
        finally:
            connectedSocketLock.release()

        while(1):
            try:
                print("R")
                lenBytes=self.request.recv(4)

                if not lenBytes:
                    break

                len = int.from_bytes(bytearray(lenBytes), byteorder='little', signed=False)
                data = self.request.recv(len)

                if not data:
                    break
                print("P")
                o=Steering()
                o.ParseFromString(data)
                values = bytearray(o.command,'ascii')
                ser.write(values)
                print(o.command)
            except:
                print("R Exception")
                print(traceback.format_exc())
                break;
        self.request.close()
        print("Q1")
        connectedSocketLock.acquire()
        try:
            connectedSocket=None
        finally:
            connectedSocketLock.release()
        print("Q2")

def startServer():
    with socketserver.TCPServer((HOST, PORT), MyTCPHandler) as server:
        server.serve_forever()




if __name__ == "__main__":
    ser = serial.Serial('/dev/ttyUSB0',1200,rtscts=0)
    HOST, PORT = "your ip", 9997
    t=Thread(target=startServer)
    t.start()
    cap = cv2.VideoCapture(0)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, 2560)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)

    while(1):
        connectedSocketLock.acquire()
        try:
            if(connectedSocket!=None):
                try:
                    for i in range(3):
                        cap.read()
                    ret, img = cap.read()
                    #retval=array.array('i',cv2.imencode(".png",img)[1])

                    x=Image()
                    x.format="jpg"
                    x.data=cv2.imencode(".jpg",img[:,0:1024])[1].tobytes()
                    msg=x.SerializeToString()
                    lmsg=len(msg).to_bytes(4, byteorder='little')
                    connectedSocket.send(lmsg)
                    connectedSocket.send(msg)
                    print('.')
                except:
                        print("Send Exception")
                        print(traceback.format_exc())
                        connectedSocket=None
        finally:
            connectedSocketLock.release()

        #time.sleep(10)

