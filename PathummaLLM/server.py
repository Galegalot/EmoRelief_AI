"""
Flask Server for EmoRelief Game
Receives telemetry from Unity C# and queries Pathumma LLM
Location: D:\\SAU\\Year2\\Group Work\\EmoReliefAI\\PathummaLLM\\server.py
"""

import json
from flask import Flask, request, jsonify
from emotion_analyzer import analyze_gameplay

app = Flask(__name__)

# ตั้งค่า CORS Header แบบ Native ไม่ต้องพึ่งไลบรารีภายนอก
@app.after_request
def add_cors_headers(response):
    response.headers["Access-Control-Allow-Origin"] = "*"
    response.headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization"
    response.headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS"
    return response

@app.route("/health", methods=["GET"])
def health_check():
    """Endpoint สำหรับ Unity ตรวจสอบว่า Python Server สแตนด์บายพร้อมหรือยัง"""
    return jsonify({"status": "ok", "service": "EmoRelief AI Backend (Pathumma)"}), 200

@app.route("/analyze", methods=["POST", "OPTIONS"])
def analyze():
    """
    รับ Telemetry จาก Unity และส่งเข้า Pathumma LLM
    ส่งผลลัพธ์ JSON สำหรับหน้า EMO Tips กลับให้ Unity
    """
    if request.method == "OPTIONS":
        return jsonify({"status": "preflight_ok"}), 200

    try:
        data = request.get_json(force=True)
        if not data:
            return jsonify({"error": "No JSON payload provided"}), 400

        location = data.get("location", "")
        language = data.get("language", "th")

        # ส่งเข้าฟังก์ชันวิเคราะห์ (language: "en" หรือ "th")
        result = analyze_gameplay(data, location=location, language=language)
        return jsonify(result), 200

    except Exception as e:
        print(f"❌ Server Error: {e}")
        return jsonify({"error": str(e)}), 500

if __name__ == "__main__":
    print("=" * 60)
    print("🚀 เริ่มต้นเซิร์ฟเวอร์ EmoRelief AI (Flask) ที่ http://127.0.0.1:8000")
    print("📡 พร้อมรับคำขอจาก Unity C# ...")
    print("=" * 60)
    app.run(host="127.0.0.1", port=8000, debug=False)
