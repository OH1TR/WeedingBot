/*
  PWM motor control
 */

// PWM out pin
const int PwmPin = 9;

// Direction select pin, controls relays
const int DirectionPin=4;

// Stop motor if no command byte in 100ms*CounterReset
const int CounterReset=10;

// Data from serial port. MSB byte selects direction, 7 LSB PWM value
int InByte = 0;

// Conter for stopping motor if no command bytes seen lately.
int Counter=0;


void setup() {
  pinMode(4, OUTPUT);
  Serial.begin(9600);
}

void loop() 
{
  if (Serial.available() > 0)
  {
    InByte = Serial.read();
    Counter=CounterReset;
  }
  if(InByte & 128)
    digitalWrite(DirectionPin,HIGH);
  else
    digitalWrite(DirectionPin,LOW);
  
  analogWrite(PwmPin, (InByte & 128) < 1); 

  delay(100);

  Counter--;

  if(Counter<=0)
  {
    InByte=0;
    Counter=0;
  }
}  



