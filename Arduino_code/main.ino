#include <VarSpeedServo.h>
#include <math.h>

VarSpeedServo servo1; 
VarSpeedServo servo2; 
VarSpeedServo servo3; 
VarSpeedServo servo4; 
VarSpeedServo servo5; 
VarSpeedServo servo6; 

const int pin1 = 10;
const int pin2 = 9;
const int pin3 = 2;
const int pin4 = 3;
const int pin5 = 4;
const int pin6 = 5;

int val1 = 0;
int val2 = 102;
int val3 = 142;
int val4 = 114;
int val5 = 98;
int val6 = 120;

int spd1 = 50;
int spd2 = 50;
int spd3 = 50;
int spd4 = 50;
int spd5 = 50;
int spd6 = 50;

const float d1 = 17.5;
const float L2 = 12.0;
const float L3 = 22.0;

const float offset1 = 88.5;
const float offset2 = 12.5;
const float offset3 = 26.5;

const int GRIP    = 90;
const int RELEASE = 150;

String buf = "";

// Tốc độ từ C# sang tốc độ servo (10->255)
int pct2spd(int speed) {
    int spd = constrain(speed, 10, 100); //giới hạn
    return map(spd, 10, 100, 10, 255);
}
void moveAll6(int t1, int t2, int t3, int t4, int t5, int t6, bool wait = true) {
    servo1.write(t1, pct2spd(spd1), false);
    servo2.write(t2, pct2spd(spd2), false);
    servo3.write(t3, pct2spd(spd3), false);
    servo4.write(t4, pct2spd(spd4), false);
    servo5.write(t5, pct2spd(spd5), false);
    // Trục 6 chặn luồng (wait) 
    servo6.write(t6, pct2spd(spd6), wait);
    // Cập nhật giá trị
    val1 = t1; val2 = t2; val3 = t3; val4 = t4; val5 = t5; val6 = t6;
}
void setup() {
    Serial.begin(115200);
    servo1.attach(pin1); servo2.attach(pin2); servo3.attach(pin3);
    servo4.attach(pin4); servo5.attach(pin5); servo6.attach(pin6);

    servo1.write(val1, pct2spd(50), false);
    servo2.write(val2, pct2spd(50), false);
    servo3.write(val3, pct2spd(50), false);
    servo4.write(val4, pct2spd(50), false);
    servo5.write(val5, pct2spd(50), false);
    servo6.write(val6, pct2spd(50), true); // Chặn luồng đợi trục cuối hoàn tất

    reply(0); // Báo C# sẵn sàng
}


//MOVE,t1,t2,t3,t4,t5,t6 \n     
//SET_SPEED,s1,s2,s3,s4,s5,s6 \n 
//GRIP                           
//RELEASE                  
//PICK,x,y,z,shape \n(S/R/C/P)
//PICK,x,y,z,? \n(chạy IK thủ công)
// ═══════════════════════════════════════════════════════════════════════
void loop() {
    while (Serial.available()) {
        char c = Serial.read(); // Đọc từng ký tự từ Serial
        if (c == '\n') {        // Gặp ký tự xuống dòng -> kết thúc 1 gói tin
            buf.trim();         // Xóa khoảng trắng thừa đầu/cuối
            if (buf.length() == 0) return; // Chuỗi rỗng -> bỏ qua

            char tmp[128];  //chuỗi gửi xuống
            buf.toCharArray(tmp, 128);         // Chuyển String của buf sang mảng char để dùng strtok
            char* header = strtok(tmp, ",");   // cắt (strtok) tên lệnh đứng trước dấu phẩy đầu tiên

            if (header != NULL) {

                // MOVE,t1,t2,t3,t4,t5,t6
                if (strcmp(header, "MOVE") == 0) {    //strcmp:so sánh chuỗi
                    reply(1); // Báo BUSY
                    char* t1 = strtok(NULL, ","); char* t2 = strtok(NULL, ","); // dùng null để đọc tiếp đoạn sau khi cắt
                    char* t3 = strtok(NULL, ","); char* t4 = strtok(NULL, ",");
                    char* t5 = strtok(NULL, ","); char* t6 = strtok(NULL, ",");

                    if (t1 && t2 && t3 && t4 && t5 && t6) {
                        // atoi: chuyển chuỗi sang số nguyên
                        moveAll6(
                            constrain(atoi(t1), 0, 180),
                            constrain(atoi(t2), 0, 180),
                            constrain(atoi(t3), 0, 180),
                            constrain(atoi(t4), 0, 180),
                            constrain(atoi(t5), 0, 180),
                            constrain(atoi(t6), 0, 180)
                        );
                    }
                    reply(0); // Báo done
                }

                // SET_SPEED,s1,s2,s3,s4,s5,s6
                else if (strcmp(header, "SET_SPEED") == 0) {
                    char* s1 = strtok(NULL, ","); char* s2 = strtok(NULL, ",");
                    char* s3 = strtok(NULL, ","); char* s4 = strtok(NULL, ",");
                    char* s5 = strtok(NULL, ","); char* s6 = strtok(NULL, ",");

                    if (s1) spd1 = constrain(atoi(s1), 10, 100);
                    if (s2) spd2 = constrain(atoi(s2), 10, 100);
                    if (s3) spd3 = constrain(atoi(s3), 10, 100);
                    if (s4) spd4 = constrain(atoi(s4), 10, 100);
                    if (s5) spd5 = constrain(atoi(s5), 10, 100);
                    if (s6) spd6 = constrain(atoi(s6), 10, 100);
                    reply(0);
                }

                //GRIP
                else if (strcmp(header, "GRIP") == 0) {
                    grip(GRIP);
                    reply(0);
                }

                //RELEASE
                else if (strcmp(header, "RELEASE") == 0) {
                    grip(RELEASE);
                    reply(0);
                }

                //PICK,x,y,z,shapeCode
                else if (strcmp(header, "PICK") == 0) {
                    reply(1);
                    char* sx = strtok(NULL, ",");
                    char* sy = strtok(NULL, ",");
                    char* sz = strtok(NULL, ",");
                    char* ss = strtok(NULL, ",");

                    if (sx && sy && sz) {
                        float x = atof(sx);  //chuyển sang số thực
                        float y = atof(sy);
                        float z = atof(sz);
                        char shapeCode = (ss != NULL) ? ss[0] : '?';

                        if (shapeCode=='S' || shapeCode=='R' || shapeCode=='C' || shapeCode=='P') {
                            pickup(x, y, z, shapeCode); 
                        } else {
                            int a, b, c;
                            if (calculate_ik(x, y, z, a, b, c)) {
                                moveAll6(a, b, c, val4, val5, val6, true);
                            }
                        }
                    }
                    reply(0);
                }            

            }             
            buf = "";
        } else {
            buf += c;
        }
    }
}
// servo.write(value, speed, wait)
// wait=true: dừng chương trình đợi servo tới đích, wait=false: servo quay chương trình cứ chạy

void grip(int position) {
    moveAll6(val1, val2, val3, val4, val5, position, true);
}

void goHome() {
    moveAll6(0, 102, 142, 114, 98, val6, true);
}

void reply(int statusCode) {
    Serial.print(statusCode); Serial.print(",");
    Serial.print(val1);        Serial.print(",");
    Serial.print(val2);        Serial.print(",");
    Serial.print(val3);        Serial.print(",");
    Serial.print(val4);        Serial.print(",");
    Serial.print(val5);        Serial.print(",");
    Serial.println(val6);
}

// IK
bool calculate_ik(float x, float y, float z, int& servoA, int& servoB, int& servoC) {  // tham chiếu
    float r = sqrt(x * x + y * y);
    float s = z - d1;
    float D = (r * r + s * s - L2 * L2 - L3 * L3) / (2.0 * L2 * L3);

    if (D > 1.0 || D < -1.0) return false; // Vượt tầm cánh tay
    float th3 = atan2(-sqrt(max(0.0, 1.0 - D * D)), D);
    float th2 = atan2(s, r) - atan2(L3 * sin(th3), L2 + L3 * cos(th3));
    servoA = constrain((int)round(atan2(y, x) * 180.0 / M_PI) + offset1, 0, 180);
    servoB = constrain((int)round(th2 * 180.0 / M_PI) + offset2, 0, 180);
    servoC = constrain((int)round(-th3 * 180.0 / M_PI) + offset3, 0, 180);
    return true;
}
// Chu trình 
void pickup(float x, float y, float z, char shapeCode) {
    int a, b, c, as, bs, cs;
    if (!calculate_ik(x, y, z, a, b, c)) return; //vượt tầm tay -> thoát
    bool haveSafe = calculate_ik(x, y, 6.0, as, bs, cs); // độ cao an toàn 6cm

    //tới phía trên vật ở độ cao an toàn, mở kẹp
    if (haveSafe) moveAll6(as, bs, cs, val4, val5, RELEASE, true);
    delay(800); 

   //hạ xuống vật
    moveAll6(a, b, c, val4, val5, RELEASE, true);
    delay(500); 
    grip(GRIP);
    delay(500);
    //nhấc lên an toàn
    if (haveSafe) moveAll6(as, bs, cs, val4, val5, GRIP, true);
    delay(400);


    int targetA = 0, targetB = 102, targetC = 142; //Home
    bool validShape = false;
    if      (shapeCode == 'S') validShape = calculate_ik(0,10.0, 10.0, targetA, targetB, targetC); // SQUARE
    else if (shapeCode == 'R') validShape = calculate_ik(0,15.0, 15.0, targetA, targetB, targetC); // RECTANGLE
    else if (shapeCode == 'C') validShape = calculate_ik(0,20.0, 20.0, targetA, targetB, targetC); // CIRCLE
    else if (shapeCode == 'P') validShape = calculate_ik(0,25.0, 20.0, targetA, targetB, targetC); // PYRAMID

    // Không có khay đích hợp lệ -> về Home rồi nhả
    if (!validShape || shapeCode == '?') {
        goHome();
        delay(300);
        grip(RELEASE);
        delay(300);
        return;
    }

    //Đưa vật tới khay đích
    moveAll6(targetA, targetB, targetC, val4, val5, GRIP, true);
    delay(400); // Chờ ổn định trước khi nhả

    grip(RELEASE);
    delay(300);
    goHome();
}
