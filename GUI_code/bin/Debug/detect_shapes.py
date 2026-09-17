import cv2
import json
import time
import threading # giao tiếp cả python và mở server trả lời C#
import numpy as np
from ultralytics import YOLO
from http.server import BaseHTTPRequestHandler, HTTPServer #tạo webserver thu nhỏ
from socketserver import ThreadingMixIn

MODEL_PATH  = "my_model.pt"
SERVER_PORT = 5000
CONF        = 0.5

# Ma trận perspective
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

#Bộ nhớ đệm dùng chung giữa ai_thread và HTTP server
frame_lock    = threading.Lock() #khi AI cập nhật hình ảnh tọa độ mới thì mới mở để lấy dữ liệu
output_frame  = None   # JPEG bytes để stream MJPEG
latest_shape  = "---"  # Tên hình gần nhất ("---" = không thấy vật)
latest_x      = 0.0
latest_y      = 0.0
ai_running    = threading.Event()
ai_running.set()


def pixel_to_real(cx, cy):
    pt = np.array([[[cx, cy]]], dtype="float32")
    r  = cv2.perspectiveTransform(pt, M)
    return round(float(r[0][0][0]), 1), round(float(r[0][0][1]), 1)

#Load YOLO 
print("Đang tải YOLO model...")
model = YOLO(MODEL_PATH)
print("Model sẵn sàng.")

#Thread xử lý AI (tự restart khi camera bị ngắt) 
def ai_thread():
    global output_frame, latest_shape, latest_x, latest_y

    while ai_running.is_set():
        cap = cv2.VideoCapture(1, cv2.CAP_DSHOW)
        #cap = cv2.VideoCapture(1)
        cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        cap.set(cv2.CAP_PROP_FRAME_WIDTH,  320)
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 240)

        if not cap.isOpened():
            print("!!! Không mở được webcam, thử lại sau 3 giây...")
            time.sleep(3)
            continue

        print("Camera đã mở, bắt đầu detect...")

        while ai_running.is_set():
            # Xả buffer camera (lấy frame mới nhất, bỏ frame cũ)
            for _ in range(3):
                cap.grab() #chạy lệnh grab 3 lần liên tiếp bỏ đi 3 khung hình cũ

            ret, frame = cap.read()
            if not ret:
                print("!!! Mất kết nối camera, đang thử kết nối lại...")
                break  # Thoát vòng trong, vòng ngoài sẽ mở lại camera

            results = model(frame, conf=CONF, verbose=False) #chạy yolo

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
                    #cy=int(y2)
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

# HTTP Handler
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

#Server đa luồng
class ThreadedServer(ThreadingMixIn, HTTPServer):
    daemon_threads = True

# Entry point
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
