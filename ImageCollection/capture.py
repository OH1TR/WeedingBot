import sys
import cv2
import serial
import time
import os

def main(argv):

    folder=time.strftime("%Y%m%d_%H%M%S")

    if not os.path.exists(folder):
        os.makedirs(folder)

    imageno = 0

    ser = serial.Serial('/dev/ttyUSB0',1200,rtscts=0)

    cap = cv2.VideoCapture(0)
    while True:
        for i in range(3):
            cap.read()

        ret, img = cap.read()

        imageno = imageno + 1
        f = os.path.join(folder, str(imageno)+'.jpg')
        cv2.imwrite(f,img)

        values = bytearray([40])
        ser.write(values)
        print('.')

    ser.write(0)
    cv2.destroyAllWindows()
    cv2.VideoCapture(0).release()

if __name__ == '__main__':
    main(sys.argv)
