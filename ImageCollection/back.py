import sys
import serial
import time

def main(argv):

    ser = serial.Serial('/dev/ttyUSB0',1200,rtscts=0)

    while True:
        values = bytearray([255])
        ser.write(values)
        print('.')
        time.sleep(0.5)

if __name__ == '__main__':
    main(sys.argv)
