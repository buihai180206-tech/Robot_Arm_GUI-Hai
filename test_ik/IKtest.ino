#include <Servo.h>
#include <Math.h>
Servo servo1;    // theta1 
Servo servo2; // theta2
Servo servo3;    // theta3
Servo servo4;
Servo servo5;
Servo servo6;
//Thông số robot
const float d1 = 17.5;
const float L2 = 12.0; 
const float L3 = 22;//21.5;  
//Offset của servo
const float offset1 = 88.5;//78.0;//90;//87.7; //81  
const float offset2 = 12.5;//3.5;//18.9;//23.5; giảm-> hạ xg
const float offset3 = 27.5;//3.5;//41.6;//40.4; tăng -> hạ xj chuan 100%
//Giới hạn servo
const int servoMin = 0;
const int servoMax = 180;


float clampf(float v, float a, float b){
  if(v < a) return a;
  if(v > b) return b;
  return v;
}

void setup(){
  Serial.begin(115200);
  servo1.attach(10);      
  servo2.attach(9);
  servo3.attach(2);
  servo4.attach(3);      
  servo5.attach(4);
  servo6.attach(5);
  //servo1.write(180);
  //servo2.write(120);
  //servo3.write(135);
 servo1.write(14);
  servo2.write(121);
  servo3.write(144);
  servo4.write(114);
  servo5.write(98);
  servo6.write(122);
  Serial.println("Nhap x y z");

}
void loop(){
  
  if(Serial.available()){
    float x = Serial.parseFloat(); 
    float y = Serial.parseFloat();   //đọc từ giá trị từ serial
    float z = Serial.parseFloat();
  //  while (Serial.available() && Serial.peek() != '\n') Serial.read();
    //if (Serial.peek() == '\n') Serial.read();
    
    while (Serial.read() != '\n');
    // Tính IK :
    float r = sqrt(x*x + y*y);
    float s = z - d1;
    float D = (r*r + s*s - L2*L2 - L3*L3) / (2.0 * L2 * L3);
   if (D > 1.0 || D < -1.0) {
     Serial.println("Không tới");
      return;
    }
    //-sqrt hoac +sqrt 
    float th3 = atan2(-sqrt(max(0.0, 1.0 - D*D)), D); 
    float num = L3 * sin(th3);
    float den = L2 + L3 * cos(th3);
    float th2 = atan2(s, r) - atan2(num, den); 
    float th1 = atan2(y, x);
    // Chuyển sang độ
    float a1 = th1 * 180.0 / M_PI;
    float a2 = th2 * 180.0 / M_PI;
    float a3 = th3 * 180.0 / M_PI;
    //Chỉnh theo offset
    int servoA = (int)round(clampf(a1 + offset1, servoMin, servoMax));
    int servoB = (int)round(clampf(a2 + offset2, servoMin, servoMax));
    int servoC = (int)round(clampf(-a3 + offset3, servoMin, servoMax));

    Serial.print("IK angles: ");
    Serial.print(a1); Serial.print(", ");
    Serial.print(a2); Serial.print(", ");
    Serial.println(a3);

    Serial.print("Servo write: ");
    Serial.print(servoA); Serial.print(", ");
    Serial.print(servoB); Serial.print(", ");
    Serial.println(servoC);

    servo1.write(servoA);
    servo2.write(servoB);
    servo3.write(servoC);
    delay(100);
  }
}
/*
#include <Servo.h>
Servo servo1;
void setup() {
Serial.begin(115200);
servo1.attach(4);

}

void loop() {
for(int i=0;i<=180;i+=20){
    servo1.write(i);
    Serial.println(i);
    delay(1000);
  }
}
 */