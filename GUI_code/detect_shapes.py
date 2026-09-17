import cv2
import json
import time
import threading
import numpy as np
from ultralytics import YOLO
from http.server import BaseHTTPRequestHandler, HTTPServer
from socketserver import ThreadingMixIn

# ── Cấu hình ────────────────────────────────────────────────────────────────
MODEL_PATH  = "my_model.pt"
CAM_INDEX   = 1      # Đổi thành 0 nếu chỉ có 1 webcam
SERVER_PORT = 5000
CONF        = 0.5

# ── Ma trận perspective: pixel → tọa độ thực (cm)
pts_src = np.array([
    [ 43,  43],   # Trái-Trên
    [592,  44],   # Phải-Trên
    [587, 447],   # Phải-Dưới
    [ 36, 450],   # Trái-Dưới
], dtype="float32")

pts_dst = np.array([
    [12.5, -12.5],
    [12.5,  12.5],
    [30.0,  10.0],
    [30.0, -10.0],
], dtype="float32")

M = cv2.getPerspectiveTransform(pts_src, pts_dst)

# ── Bộ nhớ đệm dùng chung giữa ai_thread và HTTP server ────────────────────
frame_lock    = threading.Lock()
output_frame  = None   # JPEG bytes để stream MJPEG
latest_shape  = "---"  # Tên hình gần nhất ("---" = không thấy vật)
latest_x      = 0.0
latest_y      = 0.0
ai_running    = threading.Event()
ai_running.set()

# ── Helpers ──────────────────────────────────────────────────────────────────
def pixel_to_real(cx, cy):
    pt = np.array([[[cx, cy]]], dtype="float32")
    r  = cv2.perspectiveTransform(pt, M)
    return round(float(r[0][0][0]), 1), round(float(r[0][0][1]), 1)

# ── Load YOLO ────────────────────────────────────────────────────────────────
print("Đang tải YOLO model...")
model = YOLO(MODEL_PATH)
print("Model sẵn sàng.")

# ── Thread xử lý AI (tự restart khi camera bị ngắt) ─────────────────────────
def ai_thread():
    global output_frame, latest_shape, latest_x, latest_y

    while ai_running.is_set():
       #cap = cv2.VideoCapture(CAM_INDEX, cv2.CAP_DSHOW)
        cap = cv2.VideoCapture(1)
        cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        cap.set(cv2.CAP_PROP_FRAME_WIDTH,  640)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

        if not cap.isOpened():
            print("!!! Không mở được webcam, thử lại sau 3 giây...")
            time.sleep(3)
            continue

        print("Camera đã mở, bắt đầu detect...")

        while ai_running.is_set():
            # Xả buffer camera (lấy frame mới nhất, bỏ frame cũ)
            for _ in range(3):
                cap.grab()

            ret, frame = cap.read()
            if not ret:
                print("!!! Mất kết nối camera, đang thử kết nối lại...")
                break  # Thoát vòng trong, vòng ngoài sẽ mở lại camera

            results = model(frame, conf=CONF, verbose=False)

            # Tìm vật gần gốc tọa độ robot nhất
            best_shape   = "---"
            best_x       = 0.0
            best_y       = 0.0
            min_dist_sq  = float('inf')

            for result in results:
                for box in result.boxes:
                    x1, y1, x2, y2 = box.xyxy[0].cpu().numpy()
                    cls  = int(box.cls[0])
                    name = model.names[cls].strip().upper()

                    cx = int((x1 + x2) / 2)
                    cy = int((y1 + y2) / 2)
                    real_x, real_y = pixel_to_real(cx, cy)
                    real_x=round(real_x)
                    real_y=round(real_y)
                    # Vẽ lên frame để hiển thị camera
                    cv2.rectangle(frame, (int(x1), int(y1)), (int(x2), int(y2)), (0, 255, 0), 2)
                    cv2.circle(frame, (cx, cy), 5, (0, 0, 255), -1)
                    cv2.putText(
                        frame,
                        f"{name} ({real_x},{real_y})",
                        (int(x1), int(y1) - 10),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 2
                    )

                    # Chọn vật gần robot nhất (dùng bình phương khoảng cách, tránh sqrt)
                    dist_sq = real_x ** 2 + real_y ** 2
                    if dist_sq < min_dist_sq:
                        min_dist_sq = dist_sq
                        best_shape  = name
                        best_x      = real_x
                        best_y      = real_y

            # Ghi timestamp lên frame cho dễ debug
            ts = time.strftime("%H:%M:%S")
            cv2.putText(frame, ts, (10, 20),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 255), 1)

            # Cập nhật bộ nhớ đệm dùng chung
            _, buf = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
            with frame_lock:
                latest_shape = best_shape
                latest_x     = best_x
                latest_y     = best_y
                output_frame = buf.tobytes()

        cap.release()
        if ai_running.is_set():
            time.sleep(2)  # Chờ trước khi mở lại camera

# ── HTTP Handler ─────────────────────────────────────────────────────────────
class StreamHandler(BaseHTTPRequestHandler):

    def log_message(self, *args):
        pass  # Tắt log HTTP mặc định để console sạch hơn

    def do_GET(self):

        # /video_feed — MJPEG stream cho C# hiển thị lên picCamera
        if self.path == '/video_feed':
            self.send_response(200)
            self.send_header('Content-type', 'multipart/x-mixed-replace; boundary=frame')
            self.end_headers()
            try:
                while True:
                    with frame_lock:
                        frame = output_frame
                    if frame is None:
                        time.sleep(0.05)
                        continue
                    self.wfile.write(
                        b'--frame\r\n'
                        b'Content-Type: image/jpeg\r\n\r\n'
                        + frame + b'\r\n'
                    )
                    time.sleep(0.033)  # ~30fps
            except Exception:
                pass  # Client ngắt kết nối → thoát bình thường

        # /detect — C# poll để lấy tên hình + tọa độ thực tế
        elif self.path == '/detect':
            with frame_lock:
                shape = latest_shape
                x_val = latest_x
                y_val = latest_y

            # Trả "---" rõ ràng khi chưa detect được vật nào
            data = {
                "shape": shape,   # "---" nếu không thấy vật
                "x":     x_val,
                "y":     y_val
            }
            body = json.dumps(data).encode('utf-8')

            self.send_response(200)
            self.send_header('Content-Type',   'application/json')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        else:
            self.send_response(404)
            self.end_headers()

# ── Server đa luồng ──────────────────────────────────────────────────────────
class ThreadedServer(ThreadingMixIn, HTTPServer):
    daemon_threads = True

# ── Entry point ──────────────────────────────────────────────────────────────
if __name__ == '__main__':
    # Khởi động thread AI ngầm
    t = threading.Thread(target=ai_thread, daemon=True)
    t.start()

    # Chờ camera và model sẵn sàng trước khi nhận request
    print("Chờ camera khởi động...")
    time.sleep(2)

    server = ThreadedServer(('127.0.0.1', SERVER_PORT), StreamHandler)
    print(f">>> Server API hoạt động tại: http://127.0.0.1:{SERVER_PORT}")
    print(f"    /video_feed — MJPEG stream")
    print(f"    /detect     — JSON tọa độ vật gần nhất")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nĐang dừng server...")
        ai_running.clear()  # Báo thread AI dừng sạch
        t.join(timeout=3)
        server.server_close()
        print("Đã dừng.")
"""
import cv2
import os
from ultralytics import YOLO
#from roboflow import Roboflow

# --- 1. CẤU HÌNH ---

# Địa chỉ IP của ESP32-CAM
stream_url = "http://192.168.1.4:81/stream" 

# --- 2. TẢI MODEL TỪ ROBOFLOW (Chỉ chạy 1 lần) ---
model_path = "my_model.pt"

model = YOLO(model_path)

print(f"Đang kết nối tới Camera: {stream_url}")
cap = cv2.VideoCapture(stream_url)

if not cap.isOpened():
    print("Lỗi: Không thể kết nối ESP32-CAM. Hãy kiểm tra lại IP và Wifi.")
    exit()

# --- 4. VÒNG LẶP XỬ LÝ ---
cap.set(cv2.CAP_PROP_BUFFERSIZE, 1) # Giảm độ trễ

while True:
    ret, frame = cap.read()
    if not ret:
        print("Mất tín hiệu frame.")
        break

    # --- NHẬN DIỆN ---
    # conf=0.5: Chỉ hiện vật thể chắc chắn trên 50%
    results = model(frame, conf=0.5, verbose=False)

    # Vẽ khung chữ nhật lên ảnh
    annotated_frame = results[0].plot()

    # Hiển thị
    cv2.imshow('ESP32-CAM - Roboflow Detection', annotated_frame)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
"""
"""
import cv2
import numpy as np
from flask import Flask, Response, jsonify
import threading
import time

app = Flask(__name__)

cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)  # FIX 1: thêm CAP_DSHOW cho Windows
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

latest_frame = None
latest_shape = "---"
frame_lock = threading.Lock()

def detect_shape(contour):
    peri = cv2.arcLength(contour, True)
    approx = cv2.approxPolyDP(contour, 0.04 * peri, True)
    verts = len(approx)
    if verts == 3:
        return "PYRAMID"
    elif verts == 4:
        x, y, w, h = cv2.boundingRect(approx)
        return "SQUARE" if 0.9 <= w/float(h) <= 1.1 else "RECTANGLE"
    else:
        return "CIRCLE"

def capture_loop():
    global latest_frame, latest_shape
    while True:
        ret, frame = cap.read()
        if not ret:
            time.sleep(0.05)  # FIX 2: tránh vòng lặp rỗng khi mất frame
            continue

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        blurred = cv2.GaussianBlur(gray, (5, 5), 0)
        _, thresh = cv2.threshold(blurred, 60, 255, cv2.THRESH_BINARY)
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        detected = "---"
        for cnt in contours:
            if cv2.contourArea(cnt) < 1500:
                continue
            shape = detect_shape(cnt)
            detected = shape
            x, y, w, h = cv2.boundingRect(cnt)
            cv2.rectangle(frame, (x, y), (x+w, y+h), (0, 255, 0), 2)
            cv2.putText(frame, shape, (x, y-10),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.7, (0, 255, 0), 2)

        _, buf = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 80])
        with frame_lock:
            latest_shape = detected
            latest_frame = buf.tobytes()

def gen_frames():
    while True:
        with frame_lock:
            frame = latest_frame
        if frame is None:
            time.sleep(0.01)  # FIX 3: nhường CPU khi chưa có frame
            continue
        yield (b'--frame\r\n'
               b'Content-Type: image/jpeg\r\n\r\n' + frame + b'\r\n')
        time.sleep(0.033)  # FIX 4: giới hạn ~30fps, tránh flood browser

@app.route('/video_feed')
def video_feed():
    return Response(gen_frames(),
                    mimetype='multipart/x-mixed-replace; boundary=frame')

@app.route('/detect')
def detect():
    with frame_lock:
        shape = latest_shape
    return jsonify({"shape": shape})

if __name__ == '__main__':
    t = threading.Thread(target=capture_loop, daemon=True)
    t.start()
    time.sleep(1)  # FIX 5: đợi capture_loop có frame đầu tiên trước khi Flask nhận request
    print(">>> Stream sẵn sàng tại http://127.0.0.1:5001/video_feed")
    app.run(host='127.0.0.1', port=5001, threaded=True)
"""