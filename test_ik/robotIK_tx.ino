#include<Wire.h>
#include<Adafruit_GFX.h>
#include<Adafruit_SSD1306.h>

#include <SPI.h>
#include <nRF24L01.h>
#include <RF24.h>
RF24 hai(7,8); 
Adafruit_SSD1306 oled(128,64,&Wire,-1);

const byte diachi[6] = "12345"; 
int joy1x=A0,  joy1y=A1,  joy2x=A2,  joy2y=A3;
int led= 5;

int mang[15]; 
const int len=2, xuong=3, chon=4;
const int ctluu=9,ctlam=6;
const int servo5= A6, servo6=A7;
int demchinh=0, demphu=0;
bool dem1=false;

int mode=0,speed=0,light=0,analog=0;


unsigned long debounce = 0;
unsigned long debouncedelay = 200;

void setup() 
{
  Serial.begin(115200);
  pinMode(led,OUTPUT);
  //hai.begin();
  if (!hai.begin()) 
  {
    Serial.println("Module không khởi động được...!!");
    while(1){}
  } 
    else { 
    digitalWrite(led,HIGH);
    Serial.println("!!");
    }
  hai.openWritingPipe(diachi);
  hai.setPALevel(RF24_PA_MIN);
  hai.setChannel(80);
  hai.setDataRate(RF24_250KBPS);  
 // hai.stopListening(); 

  oled.begin(SSD1306_SWITCHCAPVCC,0x3C);
  oled.clearDisplay();
  oled.setTextSize(1);
  oled.setTextColor(WHITE);

  pinMode(joy1x, INPUT);
  pinMode(joy1y, INPUT);
  pinMode(joy2x, INPUT);
  pinMode(joy2y, INPUT);
  pinMode(len, INPUT_PULLUP);
  pinMode(xuong, INPUT_PULLUP);
  pinMode(chon, INPUT_PULLUP);
  pinMode(servo5, INPUT);
  pinMode(servo6, INPUT);
  pinMode(ctluu, INPUT_PULLUP);
  pinMode(ctlam, INPUT_PULLUP);
  if (!hai.available()) 
  {
    Serial.println("Waiting...!!");
  } 
  manchinh();
}
void loop() 
{
   delay(50);
   hai.stopListening(); 
    mang[0] = analogRead(joy1x);
    mang[1] = analogRead(joy1y);
    mang[2] = analogRead(joy2x);
    mang[3] = analogRead(joy2y);
    mang[4] = analogRead(servo5);
    mang[5] = analogRead(servo6);
    mang[6] = mode;
    mang[7] = digitalRead(ctluu);
    mang[8] = digitalRead(ctlam);
    mang[9] = speed;
    mang[10] = light;
    mang[11] = analog;
hai.write(&mang, sizeof(mang));
Serial.println(mang[0]);
Serial.println(mang[1]);
Serial.println(mang[2]);
Serial.println(mang[3]);
Serial.println(mang[4]);
Serial.println(mang[5]);
Serial.println(mang[6]);
Serial.println(mang[7]);
Serial.println(mang[8]);
   unsigned long currentMillis = millis();
 if (digitalRead(xuong) == LOW && currentMillis - debounce > debouncedelay) {
        debounce = currentMillis;
        if (dem1 == false) {
             if(demchinh>=3){demchinh=0;}else{demchinh++;}
            manchinh();
        } else {
            if(demphu>=3){demphu=0;}else{demphu++;}
            manphu();
        }
    }
    if (digitalRead(len) == LOW && currentMillis - debounce > debouncedelay) {
        debounce = currentMillis;
        if (dem1 == false) {
            if(demchinh<=0){demchinh=3;}else{demchinh--;}
            manchinh();
        } else {
             if(demphu<=0){demphu=3;}else{demphu--;}
            manphu();
        }
    }
    if (digitalRead(chon) == LOW && currentMillis - debounce > debouncedelay) {
        debounce = currentMillis;
        if (dem1 == false) {
            dem1 = true;
            manphu();
        } else {
            dem1 = false;
            choncuoi();
        }
    }
}

void manchinh(){
if(demchinh==0){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print(">MODE");
oled.setCursor(30,20);
oled.print("SPEED");
oled.setCursor(30,35);
oled.print("LIGHT");
oled.setCursor(30,50);
oled.print("ANALOG");
oled.display();
}
if(demchinh==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("MODE");
oled.setCursor(30,20);
oled.print(">SPEED");
oled.setCursor(30,35);
oled.print("LIGHT");
oled.setCursor(30,50);
oled.print("ANALOG");
oled.display();
}
if(demchinh==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("MODE");
oled.setCursor(30,20);
oled.print("SPEED");
oled.setCursor(30,35);
oled.print(">LIGHT");
oled.setCursor(30,50);
oled.print("ANALOG");
oled.display();
}
if(demchinh==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("MODE");
oled.setCursor(30,20);
oled.print("SPEED");
oled.setCursor(30,35);
oled.print("LIGHT");
oled.setCursor(30,50);
oled.print(">ANALOG");
oled.display();
}
}
//////////
void manphu(){
if(demchinh==0 && demphu==0){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print(">Manual Control");
oled.setCursor(30,20);
oled.print("Dectect Objects");
oled.setCursor(30,35);
oled.print("Save movements");
oled.setCursor(30,50);
oled.print("Extension");
oled.display();
}
if(demchinh==0 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("Manual Control");
oled.setCursor(30,20);
oled.print(">Dectect Objects");
oled.setCursor(30,35);
oled.print("Save movements");
oled.setCursor(30,50);
oled.print("Extension");
oled.display();
}
if(demchinh==0 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("Manual Control");
oled.setCursor(30,20);
oled.print("Dectect Objects");
oled.setCursor(30,35);
oled.print(">Save movements");
oled.setCursor(30,50);
oled.print("Extension");
oled.display();
}
if(demchinh==0 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("Manual Control");
oled.setCursor(30,20);
oled.print("Dectect Objects");
oled.setCursor(30,35);
oled.print("Save movements");
oled.setCursor(30,50);
oled.print(">Extension");
oled.display();
}
////////////
if(demchinh==1 && demphu==0 ){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print(">100");
oled.setCursor(30,20);
oled.print("500");
oled.setCursor(30,35);
oled.print("1000");
oled.setCursor(30,50);
oled.print("1500");
oled.display();
}
if(demchinh==1 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("100");
oled.setCursor(30,20);
oled.print(">500");
oled.setCursor(30,35);
oled.print("1000");
oled.setCursor(30,50);
oled.print("1500");
oled.display();
}
if(demchinh==1 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("100");
oled.setCursor(30,20);
oled.print("500");
oled.setCursor(30,35);
oled.print(">1000");
oled.setCursor(30,50);
oled.print("1500");
oled.display();
}
if(demchinh==1 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("100");
oled.setCursor(30,20);
oled.print("500");
oled.setCursor(30,35);
oled.print("1000");
oled.setCursor(30,50);
oled.print(">1500");
oled.display();
}
////////////
if(demchinh==2 && demphu==0 ){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print(">ON");
oled.setCursor(30,20);
oled.print("OFF");
oled.setCursor(30,35);
oled.print("Extension");
oled.setCursor(30,50);
oled.print("Extension");
oled.display();
}
if(demchinh==2 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("ON");
oled.setCursor(30,20);
oled.print(">OFF");
oled.setCursor(30,35);
oled.print("Extension");
oled.setCursor(30,50);
oled.print("Extension");
oled.display();
}
if(demchinh==2 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("ON");
oled.setCursor(30,20);
oled.print("OFF");
oled.setCursor(30,35);
oled.print(">Extension");
oled.setCursor(30,50);
oled.print("Extension");
oled.display();
}
if(demchinh==2 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("ON");
oled.setCursor(30,20);
oled.print("OFF");
oled.setCursor(30,35);
oled.print("Extension");
oled.setCursor(30,50);
oled.print(">Extension");
oled.display();
}
if(demchinh==3 && demphu==0 ){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print(">RIGHTx");
oled.setCursor(30,20);
oled.print("LEFTx");
oled.setCursor(30,35);
oled.print("RIGHTy");
oled.setCursor(30,50);
oled.print("LEFTy");
oled.display();
}
if(demchinh==3 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("RIGHTx");
oled.setCursor(30,20);
oled.print(">LEFTx");
oled.setCursor(30,35);
oled.print("RIGHTy");
oled.setCursor(30,50);
oled.print("LEFTy");
oled.display();
}
if(demchinh==3 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("RIGHTx");
oled.setCursor(30,20);
oled.print("LEFTx");
oled.setCursor(30,35);
oled.print(">RIGHTy");
oled.setCursor(30,50);
oled.print("LEFTy");
oled.display();
}
if(demchinh==3 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("RIGHTx");
oled.setCursor(30,20);
oled.print("LEFTx");
oled.setCursor(30,35);
oled.print("RIGHTy");
oled.setCursor(30,50);
oled.print(">LEFTy");
oled.display();
}
}
///////
void choncuoi(){
if(demchinh==0 && demphu==0){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
mode=0;
}
if(demchinh==0 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
mode=1;
}
if(demchinh==0 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
mode=2;
}
if(demchinh==0 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
mode=3;
}
if(demchinh==1 && demphu==0){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
if(demchinh==1 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
if(demchinh==1 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
if(demchinh==1 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
//////////////////////////////////
if(demchinh==2 && demphu==0){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
if(demchinh==2 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
if(demchinh==2 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
if(demchinh==2 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.display();
}
///////////////////////////////
if(demchinh==3 && demphu==0){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.setCursor(30,20);
oled.print(mang[3]);
oled.display();
}
if(demchinh==3 && demphu==1){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.setCursor(30,20);
oled.print(mang[1]);
oled.display();

}
if(demchinh==3 && demphu==2){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.setCursor(30,20);
oled.print(mang[2]);
oled.display();
}
if(demchinh==3 && demphu==3){
oled.clearDisplay();
oled.setCursor(30,5);
oled.print("DONE!");
oled.setCursor(30,20);
oled.print(mang[0]);
oled.display();
}
}

