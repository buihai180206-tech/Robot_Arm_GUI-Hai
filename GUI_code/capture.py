import cv2
import time
import os

CAM_INDEX  = 1       
SAVE_DIR   = "dataset"  # thư mục lưu ảnh
os.makedirs(SAVE_DIR, exist_ok=True)

cap = cv2.VideoCapture(CAM_INDEX)
#cap.set(cv2.CAP_PROP_FRAME_WIDTH,  640)
#cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

count = 0
print("Bấm S để chụp | Bấm Q để thoát")

while True:
    ret, frame = cap.read()
    if not ret:
        break

    cv2.imshow("Chup anh training", frame)
    key = cv2.waitKey(1) & 0xFF

    if key == ord('s'):
        count += 1
        path = os.path.join(SAVE_DIR, f"img_{count:04d}_{int(time.time())}.jpg")
        cv2.imwrite(path, frame)
        print(f"[{count}] Đã lưu: {path}")

    elif key == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
print(f"Tổng cộng đã chụp {count} ảnh.")