import sys
import cv2
import serial

def main(argv):
    ser = serial.Serial()
    ser.baudrate = 19200
    ser.port = '/dev/ttyUSB0'
    ser.open()

    cap = cv2.VideoCapture(0)
    while True:
        for i in range(3):
            cap.read()
        ret, img = cap.read()
        #cv2.imshow("input", img)
        #key = cv2.waitKey(500)
        #if key == 27:
        #    break
        ser.write(65)
        print('.')

    ser.write(0)
    cv2.destroyAllWindows()
    cv2.VideoCapture(0).release()

if __name__ == '__main__':
    main(sys.argv)
