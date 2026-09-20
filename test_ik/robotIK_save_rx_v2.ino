#include <Servo.h>
#include <Math.h>
#include <SPI.h>
#include <nRF24L01.h>
#include <RF24.h>
Servo servo1;    // theta1 
Servo servo2; // theta2
Servo servo3;    // theta3
Servo servo4;
Servo servo5;
Servo servo6;
// 180, 163, 135
RF24 hai(7,8); 
const byte diachi[6] = "12345";

int mang[15];
int val1x=0,val1y=0,val2x=0,val2y=0;
int ser5=1,ser6=1;
int ttluu=1,ttlam=1;
bool ttxoa=0;
int mode=0,speed=0,light=0,analog=0;

int last1 = 0,last2 = 0;
int pos1 = 0,pos2=0;

int val1=180,val2=180,val3=90,val4=90,val5=90,val6=90;
//Thông số robot
const float d1 = 18.0;
const float L2 = 12.0; 
const float L3 = 22.0;  
//Offset của servo
const int offset1 = 90;   
const int offset2 = 90;
const int offset3 = 48;

//Giới hạn servo
const int servoMin = 0;
const int servoMax = 180;

//Lưu hành động
int a[50][6]; 
int n = 0;  

float clampf(float v, float a, float b){
  if(v < a) return a;
  if(v > b) return b;
  return v;
}

void setup(){
  Serial.begin(115200);
  hai.begin();
  if (!hai.begin()) 
  {
    Serial.println("Module không khởi động được");
    while (1) {}
  }    
  hai.openReadingPipe(0,diachi);
  hai.setPALevel(RF24_PA_MIN);
  hai.setChannel(80);
  hai.setDataRate(RF24_250KBPS);  
  //hai.startListening();
  if (!hai.available())
  {
    Serial.println("Chưa kết nối được với TX");
    Serial.println("CHỜ KẾT NỐI");
  }
  servo1.attach(10);      
  servo2.attach(9);
  servo3.attach(2);
  servo4.attach(3);      
  servo5.attach(4);
  servo6.attach(5);

  servo1.write(val1);
  servo2.write(val2);
  servo3.write(val3);
  servo4.write(val4);
  servo5.write(val5);
  servo6.write(val6);
  Serial.println("Nhap x y z");
  
}
void loop(){ 
  delay(50);
  hai.startListening();
  if (hai.available()) 
  {
    while(hai.available()){
    hai.read(&mang, sizeof(mang));
    val1x=mang[0];
    val1y=mang[1];
    val2x=mang[2];
    val2y=mang[3];
    ser5=mang[4];
    ser6=mang[5];
    mode=mang[6];
   ttluu=mang[8];
   ttlam=mang[7];
    speed=mang[9];
    light=mang[10];
    analog=mang[11];

    }
    Serial.println(val1x);
    Serial.println(val1y);
    Serial.println(val2x);
    Serial.println(val2y);
    Serial.println(ser5);
    Serial.println(ser6);
    Serial.println(ttluu);
    Serial.println(ttlam);
    Serial.println(mode);
if(mode==0 || mode==2){
if(val1x >= 600) // joystick sang phải thì mỗi lần đều cộng 2 độ//
{
  if(val1 <= 180)
  {
  val1 = val1 + 5;
  servo2.write(val1);
  }
}
else if(val1x <= 400)  
{
  if(val1 >= 0)
  {
   val1 = val1 - 5;
   servo2.write(val1);
  }
}
/////
  if(val1y >= 600) 
{
  if(val2 <= 180)
  {
  val2 = val2 + 5;
  servo1.write(val2);
  }
}
else if(val1y <= 400) 
{
  if(val2 >= 0)
  {
   val2 = val2 - 5;
   servo1.write(val2);
  }
}
///////
  if( val2x >= 600)
{
  if(val3 <= 220)
  {
  val3 = val3 + 4;
  servo3.write(val3);
  }
}
else if(val2x <= 400)
{
  if(val3 >= 0)
  {
   val3 = val3 - 4;
   servo3.write(val3);
  }
}
if( val2y >= 600)
{
  if(val4 <= 180)
  {
  val4 = val4 + 5;
  servo4.write(val4);
  }
}
else if(val2y <= 400)
{
  if(val4 >= 0)
  {
   val4 = val4 - 5;
   servo4.write(val4);
  }
}
val5= map(ser5,0,1023,0,180);
servo5.write(val5);
val6= map(ser6,0,1023,0,180);
servo6.write(val6);
///////////////  
  if (ttluu == 0 && ttxoa==0) {
    luu();
    delay(200);  
  }
  
  if (ttlam == 0) {
    lam();
    delay(200); 
    ttxoa=1; 
  }
  if (ttluu == 0 && ttxoa==1) {
    xoa();
    delay(200);  
    ttxoa=0;
  }

}

if(mode==1){
if(Serial.available()){
    float x = Serial.parseFloat(); 
    float y = Serial.parseFloat();   //đọc từ giá trị từ serial
    float z = Serial.parseFloat();
    while (Serial.available() && Serial.peek() != '\n') Serial.read();
    if (Serial.peek() == '\n') Serial.read();
    
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
if(mode==2){
  int ttluu = digitalRead(ctluu);
  int ttlam = digitalRead(ctlam);
  int ttxoa = digitalRead(ctxoa);
  
  
  if (ttluu == 0) {
    luu();
    delay(300);  
  }
  
  if (ttlam == 0) {
    lam();
    delay(300);  
  }
  if (ttxoa == 0) {
    xoa();
    delay(300);  
  }
}
}
}
*/
  }
}
void luu() {
  a[n][0] = servo1.read(); 
  a[n][1] = servo2.read();
  a[n][2] = servo3.read();
  a[n][3] = servo4.read();
  a[n][4] = servo5.read();
  a[n][5] = servo6.read(); // Lưu vị trí hiện tại của servo
  n++;
}

void lam() {
  for (int i = 0; i < n; i++) {
    servo1.write(a[i][0]); 
    servo2.write(a[i][1]);
    servo3.write(a[i][2]);
    servo4.write(a[i][3]);
    servo5.write(a[i][4]);
    servo6.write(a[i][5]); // Di chuyển servo đến vị trí đã lưu
    delay(1000); 
  }
}
void xoa() {
  n = 0;  
  
}

