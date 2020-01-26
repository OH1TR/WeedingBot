/*
  PWM motor control
 */

// PWM out pin
const int PwmPin = 5;

// Direction select pin, controls relays
const int DirectionPin=4;


const int L298In1Pin=6;
const int L298In2Pin=7;

const int OptoIn2Pin=2;
const int OptoIn3Pin=3;

// Stop motor if no command byte in 100ms*CounterReset
const int CounterReset=50;

// Data from serial port. MSB byte selects direction, 7 LSB PWM value
int InByte = 50;

// Conter for stopping motor if no command bytes seen lately.
long Counter=0;

int targetPosition=0;
int position=0;

void setup() {
  pinMode(DirectionPin, OUTPUT);
  
  pinMode(L298In1Pin, OUTPUT);
  pinMode(L298In2Pin, OUTPUT);
  
  pinMode(OptoIn2Pin, INPUT);
  pinMode(OptoIn3Pin, INPUT);

  attachInterrupt(digitalPinToInterrupt(OptoIn2Pin), &OptoISR, CHANGE);
  attachInterrupt(digitalPinToInterrupt(OptoIn3Pin), &OptoISR2, CHANGE);
  
  Serial.begin(1200);
}

void OptoISR() {
     if(digitalRead(OptoIn2Pin)==HIGH)
    {
      if(digitalRead(OptoIn3Pin)==HIGH)
        position--;
      else
        position++;
    }
  else
  {
      if(digitalRead(OptoIn3Pin)==HIGH)
        position++;
      else
        position--;
  }
}

void OptoISR2() {
  if(digitalRead(OptoIn3Pin)==HIGH)
    {
      if(digitalRead(OptoIn2Pin)==HIGH)
        position++;
      else
        position--;
    }
  else
  {
      if(digitalRead(OptoIn2Pin)==HIGH)
        position--;
      else
        position++;
  }
}


char cmdBuf[50];
int cmdBufPos=0;

int ParseInt(char *buf)
{
    int d=buf[0]=='-' ? -1 : 1;
    int s=buf[1]-'0';
    int k=buf[2]-'0';
    int y=buf[3]-'0';
    return((s*100+k*10+y)*d);
}

void processCommand(char *cmd)
{
  // Zero steering position
  if(cmd[0]=='Z')
  {
    noInterrupts();
    position=0;
    targetPosition=0;
    interrupts();
  }

  // Set steering target value
  if(cmd[0]=='S')
  {
    noInterrupts();
    targetPosition = - ParseInt(cmd+1);
    interrupts();
  }

  // Set Drive motor direction and speed
  if(cmd[0]=='W')
  {
    
    int targetDrive = ParseInt(cmd+1);
    if(targetDrive>=0)
    {
      digitalWrite(DirectionPin,LOW);
      delay(50);
      analogWrite(PwmPin,targetDrive);
    }
    else
    {
      digitalWrite(DirectionPin,HIGH);
      delay(50);
      analogWrite(PwmPin,-targetDrive);
    }
    
  }

  if(cmd[0]=='D')
  {
    Serial.print("targetPosition=");
    Serial.print(targetPosition);
    Serial.print(" Position=");
    Serial.print(position);
    Serial.print("\n");
  }
}

void loop() 
{
  if (Serial.available() > 0)
  {
    Counter=0;
    
    byte inByte = Serial.read();
    cmdBuf[cmdBufPos]=inByte;
    cmdBufPos++;

    if(inByte==13)
    {
      cmdBuf[cmdBufPos-1]=0;
      processCommand(cmdBuf);
      cmdBufPos=0;
    }
  }
  
  if(targetPosition>position)
  {   
    digitalWrite(L298In1Pin,LOW);
    digitalWrite(L298In2Pin,HIGH);
  }
  else if(targetPosition<position)
  {
    digitalWrite(L298In1Pin,HIGH);
    digitalWrite(L298In2Pin,LOW);
  }
  else if(targetPosition==position)
  {
    digitalWrite(L298In1Pin,LOW);
    digitalWrite(L298In2Pin,LOW); 
  }
  Counter++;
  if(Counter>100000)
  {
    digitalWrite(L298In1Pin,LOW);
    digitalWrite(L298In2Pin,LOW);
    targetPosition=position;
    analogWrite(PwmPin,0);
    Serial.print("All stop\n");
    Counter=0;
  }  
}  
