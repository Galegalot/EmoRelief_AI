"""
Test Script for Pathumma LLM Integration (Trimmed UI Output)
Location: D:\\SAU\\Year2\\Group Work\\EmoReliefAI\\PathummaLLM\\test_pathumma.py
"""

import json
import time
from emotion_analyzer import analyze_gameplay, API_KEY, BASE_URL, MODEL_NAME

# Mock Data จากเกมระบายความเครียด (Emo Relief Game)
MOCK_GAMEPLAY_DATA = {
    "total_fight_duration": 135.0,
    "effective_fighting_time": 118.5,
    "max_attack_speed": 9.2,
    "max_speed_duration": 15.0,
    "tools_used": {
        "หมัดชก (Punch)": 160,
        "ตบหน้า (Slap)": 85,
        "ก้อนอิฐ (Brick)": 30,
    },
    "primary_tool_used": "หมัดชก (Punch)",
    "pause_tracking": {
        "pause_count": 1,
        "total_pause_duration": 16.5,
        "style": "ลุยแหลกต่อเนื่อง แทบไม่หยุดพัก",
    },
}

def main():
    print("=" * 60)
    print("🎮 EmoRelief Game - Pathumma LLM Integration Test (Trimmed)")
    print("=" * 60)
    print(f"🔗 Base URL: {BASE_URL}")
    print(f"🤖 Model:    {MODEL_NAME}")
    print(f"🔑 API Key:  {API_KEY[:6]}...{API_KEY[-4:] if len(API_KEY) > 10 else ''}")
    print("=" * 60)

    print("\n📡 กำลังส่งข้อมูลไปยัง ThaiLLM API (Pathumma)...")
    start_time = time.time()

    try:
        result = analyze_gameplay(
            game_data=MOCK_GAMEPLAY_DATA,
            location="จังหวัดเชียงใหม่",
            timeout=45,
        )
        elapsed = time.time() - start_time
        print(f"⏱️ เวลาที่ใช้ในการประมวลผล: {elapsed:.2f} วินาที\n")

        print("=" * 60)
        print("🎉 ผลลัพธ์ UI สำหรับหน้าจอจบเกม (ไม่มี Anger Analysis แล้ว):")
        print("=" * 60)
        print(f"🔥 ระดับความโกรธ (Anger Gauge): {result.get('anger_score')} / 10")
        print(f"\n💬 คำปลอบโยน (Relief Dialogue):")
        print(f"   \"{result.get('player_relief_advice')}\"")
        
        print(f"\n🌿 3 วิธีเยียวยาอารมณ์ (Actionable Healing Cards):")
        for i, method in enumerate(result.get("healing_methods", []), 1):
            print(f"   {i}. {method}")

        print(f"\n📍 3 สถานที่แนะนำในเชียงใหม่ (Recommended Places):")
        for i, place in enumerate(result.get("recommended_places", []), 1):
            print(f"   [{i}] {place.get('name')} ({place.get('category')})")
            print(f"       👉 กิจกรรม: {place.get('recommended_activity')}")
            print(f"       💡 เหตุผล:  {place.get('reason')}")

        print("\n" + "=" * 60)
        print("📋 Final JSON Output สำหรับดึงไป Render UI:")
        print("=" * 60)
        print(json.dumps(result, ensure_ascii=False, indent=2))

    except Exception as e:
        print(f"❌ เกิดข้อผิดพลาดในการทดสอบ: {e}")

if __name__ == "__main__":
    main()
